using Api.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OpenAI;


namespace Api;

public class LinkkiAgentFactory
{
    private readonly LinkkiOptions _linkkiOptions;
    private readonly MapTools _mapTools;
    private readonly OpenAiOptions _openAiOptions;
    private readonly DefaultAzureCredential _azureCredential;

    public LinkkiAgentFactory(IOptions<LinkkiOptions> linkkiOptions, IOptions<OpenAiOptions> openAiOptions,
        MapTools mapTools, DefaultAzureCredential azureCredential)
    {
        _linkkiOptions = linkkiOptions.Value;
        _openAiOptions = openAiOptions.Value;
        _mapTools = mapTools;
        _azureCredential = azureCredential;
    }


    public async Task<AIAgent> CreateAgentAsync()
    {
        var linkkiMcpTools = (await GetLinkkiMcpToolsAsync()).Cast<AITool>();

        var agent = new AzureOpenAIClient(
                new Uri(_openAiOptions.Endpoint),
                _azureCredential)
            .GetChatClient(_openAiOptions.DeploymentName)
            .CreateAIAgent(new ChatClientAgentOptions
            {
                Name = "LinkkiAgent",
                Instructions = AgentInstructions.LinkkiAgentInstructions,
                ChatOptions = new ChatOptions
                {
                    Tools = [..linkkiMcpTools, ..GetMapTools()]
                },
                ChatMessageStoreFactory = ctx => new InMemoryChatMessageStore(
#pragma warning disable MEAI001
                    new MessageCountingChatReducer(_openAiOptions.ChatHistoryTruncationReducerTargetCount),
#pragma warning restore MEAI001
                    ctx.SerializedState,
                    ctx.JsonSerializerOptions,
                    InMemoryChatMessageStore.ChatReducerTriggerEvent.AfterMessageAdded)
            })
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "linkki-agent")
            .Build();

        return agent;
    }

    private async Task<IList<McpClientTool>> GetLinkkiMcpToolsAsync()
    {
        var linkkiMcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
            { Endpoint = new Uri(_linkkiOptions.LinkkiMcpServerUrl) }));

        var linkkiMcpTools = await linkkiMcpClient.ListToolsAsync();
        return linkkiMcpTools;
    }

    private IList<AIFunction> GetMapTools()
    {
        return
        [
            AIFunctionFactory.Create(_mapTools.FilterBusLinesOnMapAsync),
            AIFunctionFactory.Create(_mapTools.ShowBusStopOnMapAsync)
        ];
    }
}