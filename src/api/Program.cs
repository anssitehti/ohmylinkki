using System.Text.Json;
using Api;
using Api.AI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Core;
using Core.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using UserChatMessage = Api.UserChatMessage;


var builder = WebApplication.CreateBuilder(args);

var azureCredential = new DefaultAzureCredential();
builder.Services.AddSingleton(azureCredential);
builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
{
    options.Credential = azureCredential;
    options.SamplingRatio = 0.1F;
    options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});

builder.Services.AddWebPubSub(o =>
        o.ServiceEndpoint =
            new WebPubSubServiceEndpoint(new Uri(builder.Configuration["WebPubSub:Endpoint"]!), azureCredential))
    .AddWebPubSubServiceClient<LinkkiHub>();
builder.Services.AddSingleton<LinkkiHubService>();

builder.Services.AddSingleton<MapTools>();

builder.Services.AddSingleton<LinkkiAgentFactory>();

builder.Services.AddSingleton<IChatHistoryProvider, MemoryChatHistoryProvider>();

builder.Services.AddMemoryCache();

builder.Services.AddOptionsWithValidateOnStart<LinkkiOptions>()
    .Bind(builder.Configuration.GetSection("Linkki")).ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection("OpenAi")).ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<CosmosDbOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb")).ValidateDataAnnotations();

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    return new CosmosClient(options.Endpoint, azureCredential, new CosmosClientOptions()
    {
        EnableContentResponseOnWrite = false,
        ConsistencyLevel = ConsistencyLevel.Session
    });
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var memoryCache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<LinkkiService>>();
    return new LinkkiService(options.Database, options.LocationContainer, options.RouteContainer, cosmosClient,
        memoryCache, logger);
});


builder.Services.AddHostedService<LinkkiLocationImporter>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/healthz/startup");
app.MapHealthChecks("/healthz/readiness");
app.MapHealthChecks("/healthz/liveness");

app.MapGet("api/negotiate", ([FromQuery] string? id, WebPubSubServiceClient<LinkkiHub> service) =>
{
    if (StringValues.IsNullOrEmpty(id))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>()
        {
            { "id", ["The id is required."] }
        });
    }

    return Results.Ok(new
    {
        url = service.GetClientAccessUri(userId: id).AbsoluteUri
    });
});

app.MapPost("api/clear-chat-history",
    (ClearChatHistory clearChatHistory, IChatHistoryProvider chatHistoryProvider) =>
    {
        chatHistoryProvider.ClearHistory(clearChatHistory.UserId);
        return Results.Ok();
    }).AddEndpointFilter<ValidationFilter<ClearChatHistory>>();


app.MapPost("api/chat-agent",
    async (UserChatMessage userMessage, LinkkiAgentFactory agentFactory, IChatHistoryProvider chatHistoryProvider,
        IHttpContextAccessor httpContextAccessor) =>
    {
        if (httpContextAccessor.HttpContext != null)
        {
            httpContextAccessor.HttpContext.Items["userId"] = userMessage.UserId;
        }

        var agent = await agentFactory.CreateAgentAsync();
        var session = await chatHistoryProvider.LoadConversationAsync(userMessage.UserId, agent);
        try
        {
            var response = await agent.RunAsync(new ChatMessage(ChatRole.User, userMessage.Message), session);
            await chatHistoryProvider.SaveConversationAsync(userMessage.UserId, agent, session);
            return Results.Ok(new
            {
                message = response.Text,
            });
        }
        catch (TaskCanceledException)
        {
            return Results.StatusCode(408);
        }
    }).AddEndpointFilter<ValidationFilter<UserChatMessage>>();


app.Run();