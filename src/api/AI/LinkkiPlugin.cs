using System.ComponentModel;
using Api.Linkki;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Api.AI;

public class LinkkiPlugin
{
    private readonly Container _container;
    
    public LinkkiPlugin(CosmosClient client, IOptions<LinkkiOptions> options)
    {
        var database = client.GetDatabase(options.Value.Database);
        _container = database.GetContainer(options.Value.LocationContainer);
    }

    [KernelFunction("get_linkki_location")]
    [Description("Gets a current location of Linkki line.")]
    [return: Description("The current location of the linkki.")]
    public async Task<dynamic?> GetLocationAsync(string line, string destination)
    {
        var query = _container.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Line.Name.ToLower() == line.ToLower() && l.Vehicle.Headsign.ToLower() == destination.ToLower())
            .OrderByDescending(l => l.Timestamp).Take(1);
        Console.WriteLine(query.ToString());
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return new
                {
                    location = item.Location,
                    speed = item.Vehicle.Speed,
                    bearing = item.Vehicle.Bearing
                };
            }
        }
        return null;
    }
}