using Api;
using Azure.Identity;
using Core.Services;
using McpServer.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var azureCredential = new DefaultAzureCredential();
builder.Services.AddSingleton(azureCredential);

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

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<LinkkiService>(sp =>
{
    var options = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var memoryCache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<LinkkiService>>();
    return new LinkkiService(options.Database, options.LocationContainer, options.RouteContainer, cosmosClient,
        memoryCache, logger);
});

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<LinkkiTool>();

var app = builder.Build();

app.MapMcp();
app.Run();