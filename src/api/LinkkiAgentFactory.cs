using Api.AI;
using Core;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using ModelContextProtocol.Client;


namespace Api;

public class LinkkiAgentFactory
{
    private readonly LinkkiOptions _linkkiOptions;
    private readonly MapPlugin _mapPlugin;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenAiOptions _openAiOptions;

    public LinkkiAgentFactory(IOptions<LinkkiOptions> linkkiOptions, IOptions<OpenAiOptions> openAiOptions,
        MapPlugin mapPlugin, IServiceProvider serviceProvider)
    {
        _linkkiOptions = linkkiOptions.Value;
        _openAiOptions = openAiOptions.Value;
        _mapPlugin = mapPlugin;
        _serviceProvider = serviceProvider;
    }

    public async Task<ChatCompletionAgent> CreateAgentAsync()
    {
        var kernel = await CreateKernelAsync();
        ChatCompletionAgent agent =
            new()
            {
                Name = "LinkkiAgent",
                Instructions = AgentInstructions.LinkkiAgentInstructions,
                Kernel = kernel,
                Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
                        { RetainArgumentTypes = true }),
                    Temperature = _openAiOptions.Temperature
                })
            };
        return agent;
    }

    private async Task<Kernel> CreateKernelAsync()
    {
        var linkkiMcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
            { Endpoint = new Uri(_linkkiOptions.LinkkiMcpServerUrl + "/sse") }));

        KernelPluginCollection pluginCollection = [];
        pluginCollection.AddFromObject(_mapPlugin);

        var linkkiMcpTools = await linkkiMcpClient.ListToolsAsync();
        pluginCollection.AddFromFunctions("LinkkiMcp",
            linkkiMcpTools.Select(aiFunction => aiFunction.AsKernelFunction()));
        return new Kernel(_serviceProvider, pluginCollection);
    }
}