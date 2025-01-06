using Api;
using Api.AI;
using Api.Linkki;
using Azure.Identity;
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

builder.Services.AddOptionsWithValidateOnStart<LinkkiOptions>()
    .Bind(builder.Configuration.GetSection("LinkkiImport")).ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection("OpenAi")).ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<CosmosDbOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb")).ValidateDataAnnotations();

builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
    return new AzureOpenAIChatCompletionService(options.DeploymentName, options.Endpoint, options.ApiKey);
});

builder.Services.AddSingleton<LinkkiPlugin>();

builder.Services.AddSingleton<IChatHistoryProvider>(new MemoryChatHistoryProvider());

builder.Services.AddTransient((sp) =>
{
    KernelPluginCollection pluginCollection = [];
    pluginCollection.AddFromObject(sp.GetRequiredService<LinkkiPlugin>());
    return new Kernel(sp, pluginCollection);
});

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    return new CosmosClient(options.ConnectionString, new CosmosClientOptions()
    {
        EnableContentResponseOnWrite = false,
        ConsistencyLevel = ConsistencyLevel.Session
    });
});

builder.Services.AddHostedService<LinkkiLocationImporter>();

builder.Services.AddWebPubSub(o => o.ServiceEndpoint = new WebPubSubServiceEndpoint(new Uri(builder.Configuration["WebPubSubEndpoint"]!), azureCredential))
    .AddWebPubSubServiceClient<LinkkiHub>();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/healthz/startup");
app.MapHealthChecks("/healthz/readiness");
app.MapHealthChecks("/healthz/liveness");

app.MapGet("api/chat", async (string message,string userId, Kernel kernel, IChatHistoryProvider chatHistoryProvider) =>
{
    var chatHistory = await chatHistoryProvider.GetHistoryAsync(userId);
    chatHistory.AddUserMessage(message);

    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
    var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory,
        executionSettings: new AzureOpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        }, 
        kernel: kernel);
    chatHistory.Add(result);
    return new
    {
        message =result.Content
    };
});

app.MapGet("api/negotiate", (WebPubSubServiceClient<LinkkiHub> service, HttpContext context) =>
{
    var id = context.Request.Query["id"];
    if (StringValues.IsNullOrEmpty(id))
    {
        context.Response.StatusCode = 400;
        return null;
    }
    return new
    {
        url = service.GetClientAccessUri(userId: id).AbsoluteUri
    };
});

app.Run();