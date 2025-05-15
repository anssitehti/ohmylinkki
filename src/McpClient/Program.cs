// See https://aka.ms/new-console-template for more information

using ModelContextProtocol.Client;


Console.WriteLine("Hello, I am MCP client. Here is a list of tools available from the server: \n");

var clientTransport = new SseClientTransport(new SseClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5059/sse")
});

var client = await McpClientFactory.CreateAsync(clientTransport);

// Print the list of tools available from the server.
foreach (var tool in await client.ListToolsAsync())
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
}

var response = await client.CallToolAsync("get_available_lines");

Console.WriteLine("\nHere is a list of available lines: \n");

Console.WriteLine(response.IsError);

foreach (var content in response.Content)
{
    Console.WriteLine($"{content.Text} ({content.Data})");
}