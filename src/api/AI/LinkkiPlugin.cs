using System.ComponentModel;
using Api.Linkki;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Api.AiPlugins;

public class LinkkiPlugin
{
    private readonly Container _container;
    
    public LinkkiPlugin(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        var database = client.GetDatabase(options.Value.Database);
        _container = database.GetContainer(options.Value.Container);
    }

    [KernelFunction("get_linkki_location")]
    [Description("Gets a current location of Linkki line.")]
    [return: Description("The current location as a GeoJSON format.")]
    public async Task<GeoJson?> GetLocationAsync(string line)
    {
        var query = _container.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Line.Name.ToLower() == line.ToLower())
            .OrderByDescending(l => l.Timestamp).Take(1);
        Console.WriteLine(query.ToString());
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                Console.WriteLine(item.Timestamp + " : "+item.Location.Coordinates);
                return item.Location;
            }
        }
        return null;
    }

    
}