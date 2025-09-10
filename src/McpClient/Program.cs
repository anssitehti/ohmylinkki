// See https://aka.ms/new-console-template for more information

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;


Console.WriteLine("Hello, I am MCP client. Here is a list of tools available from the server: \n");

var clientTransport = new SseClientTransport(new SseClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5059/sse")
});

var client = await McpClientFactory.CreateAsync(clientTransport);

var tools = await client.ListToolsAsync();

if (tools.Count == 0)
{
    Console.WriteLine("No tools available on the server.");
    return;
}

Console.WriteLine($"Found {tools.Count} tools on the server.");
Console.WriteLine();

foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
}

var result = await client.CallToolAsync("get_available_lines");

Console.WriteLine("\nHere is a list of available lines: \n");

Console.WriteLine(result.IsError);

Console.WriteLine();

foreach (var content in result.Content.OfType<TextContentBlock>())
{
    Console.WriteLine($"{content.Text}");
}