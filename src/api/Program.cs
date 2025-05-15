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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;



var builder = WebApplication.CreateBuilder(args);

var azureCredential = new DefaultAzureCredential();
builder.Services.AddSingleton(azureCredential);
builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
{
    options.Credential = azureCredential;
    options.SamplingRatio = 0.1F;
    options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});
builder.Services.AddOpenTelemetry().WithMetrics(meters => meters.AddMeter("Microsoft.SemanticKernel*"));
builder.Services.AddOpenTelemetry().WithTracing(traces => traces.AddSource("Microsoft.SemanticKernel*"));


// Azure Web PubSub
builder.Services.AddWebPubSub(o =>
        o.ServiceEndpoint =
            new WebPubSubServiceEndpoint(new Uri(builder.Configuration["WebPubSub:Endpoint"]!), azureCredential))
    .AddWebPubSubServiceClient<LinkkiHub>();
builder.Services.AddSingleton<LinkkiHubService>();

// Semantic Kernel
builder.Services.AddSingleton<MapPlugin>();

builder.Services.AddSingleton<LinkkiAgentFactory>();
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
    return new AzureOpenAIChatCompletionService(options.DeploymentName, options.Endpoint, azureCredential,
        options.DeploymentName);
});


builder.Services.AddSingleton<IChatHistoryReducer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
    return new ChatHistoryTruncationReducer(targetCount: options.ChatHistoryTruncationReducerTargetCount);
});


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
    async (ClearChatHistory clearChatHistory, IChatHistoryProvider chatHistoryProvider) =>
    {
        await chatHistoryProvider.ClearHistoryAsync(clearChatHistory.UserId);
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

        var chatHistory = await chatHistoryProvider.GetAgentHistoryAsync(userMessage.UserId);

        var thread = new ChatHistoryAgentThread(chatHistory);

        try
        {
            var agent = await agentFactory.CreateAgentAsync();
            var fullMessage = "";
            await foreach (ChatMessageContent response in agent.InvokeAsync(
                               new ChatMessageContent(AuthorRole.User, userMessage.Message), thread))
            {
                fullMessage += response.Content;
            }

            return Results.Ok(new
            {
                message = fullMessage,
            });
        }
        catch (TaskCanceledException)
        {
            return Results.StatusCode(408);
        }
    }).AddEndpointFilter<ValidationFilter<UserChatMessage>>();


app.Run();