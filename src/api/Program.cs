using Api;
using Api.AI;
using Api.Linkki;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

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

// Semantic Kernel
builder.Services.AddHttpClient("OpenAI", client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAI");
    return new AzureOpenAIChatCompletionService(options.DeploymentName, options.Endpoint, azureCredential,
        options.DeploymentName, httpClient);
});
builder.Services.AddSingleton<LinkkiPlugin>();
builder.Services.AddSingleton<IChatHistoryProvider, MemoryChatHistoryProvider>();
builder.Services.AddTransient((sp) =>
{
    KernelPluginCollection pluginCollection = [];
    pluginCollection.AddFromObject(sp.GetRequiredService<LinkkiPlugin>());
    return new Kernel(sp, pluginCollection);
});

builder.Services.AddMemoryCache();

builder.Services.AddOptionsWithValidateOnStart<LinkkiOptions>()
    .Bind(builder.Configuration.GetSection("LinkkiImport")).ValidateDataAnnotations();

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

app.MapPost("api/chat",
    async (UserChatMessage message, Kernel kernel, IChatHistoryProvider chatHistoryProvider,
        IHttpContextAccessor httpContextAccessor, IOptions<OpenAiOptions> openAiOptions) =>
    {
        if (httpContextAccessor.HttpContext != null) httpContextAccessor.HttpContext.Items["userId"] = message.UserId;
        var chatHistory = await chatHistoryProvider.GetHistoryAsync(message.UserId);
        chatHistory.AddUserMessage(message.Message);

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        try
        {
            var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory,
                executionSettings: new AzureOpenAIPromptExecutionSettings()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = openAiOptions.Value.Temperature
                },
                kernel: kernel);
            chatHistory.Add(result);

            return Results.Ok(new
            {
                message = result.Content,
                usage = result.Metadata?["Usage"],
                modelId = result.ModelId
            });
        }
        catch (TaskCanceledException)
        {
            return Results.StatusCode(408);
        }
    }).AddEndpointFilter<ValidationFilter<UserChatMessage>>();

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


app.MapGet("api/test-linkki-plugin",
    ([FromQuery] string busStopName, [FromQuery] string lineName, LinkkiPlugin plugin) =>
    {
        var result = plugin.GetBusArrivalTimes(busStopName, lineName);
        return Results.Ok(result);
    });

app.Run();