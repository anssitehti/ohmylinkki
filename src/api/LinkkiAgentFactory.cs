using Api.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OpenAI.Chat;


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
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "LinkkiAgent",
                Description = AgentInstructions.LinkkiAgentInstructions,
                ChatOptions = new ChatOptions
                {
                    Tools = [..linkkiMcpTools, ..GetMapTools()]
                },
                ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions()
                {
#pragma warning disable MEAI001
                    ChatReducer =
                        new MessageCountingChatReducer(_openAiOptions.ChatHistoryTruncationReducerTargetCount),
#pragma warning restore MEAI001
                    ReducerTriggerEvent = InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent.AfterMessageAdded
                }),
            })
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "linkki-agent")
            .Build();

        return agent;
    }

    private async Task<IList<McpClientTool>> GetLinkkiMcpToolsAsync()
    {
        var clientTransport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(_linkkiOptions.LinkkiMcpServerUrl)
        });
        var linkkiMcpClient = await McpClient.CreateAsync(clientTransport);
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