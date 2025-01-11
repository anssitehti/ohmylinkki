using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Api.Linkki;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Api.AI;

public class LinkkiPlugin
{
    private readonly Container _locationContainer;
    private readonly Container _routeContainer;

    public LinkkiPlugin(CosmosClient client, IOptions<LinkkiOptions> options)
    {
        _locationContainer = client.GetContainer(options.Value.Database, options.Value.LocationContainer);
        _routeContainer = client.GetContainer(options.Value.Database, options.Value.RouteContainer);
    }

    [KernelFunction("get_linkki_location")]
    [Description("Gets a current location of linkki.")]
    [return: Description("The current location of the linkki.")]
    public async Task<LinkkiLocationDetails?> GetLocationAsync(string line, string destination)
    {
        var query = _locationContainer.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Line.Name.ToLower() == line.ToLower() &&
                        l.Vehicle.Headsign.ToLower() == destination.ToLower())
            .OrderByDescending(l => l.Timestamp).Take(1);

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return new LinkkiLocationDetails
                {
                    Location = item.Location,
                    Speed = item.Vehicle.Speed,
                    Bearing = item.Vehicle.Bearing,
                    Headsign = item.Vehicle.Headsign
                };
            }
        }

        return null;
    }

    [KernelFunction("get_route")]
    [Description("Gets the route of the line.")]
    [return:
        Description(
            "Returns the points that the bus line follows. The first and last points can either be the starting point or the destination, depending on the direction the bus is traveling.")]
    public async Task<string[]?> GetRoute(string line)
    {
        var query = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
            .Where(l => l.LineName.ToLower() == line.ToLower());
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return item.Route;
            }
        }

        return null;
    }
}

public class LinkkiLocationDetails
{
    public GeoJson Location { get; set; }
    public double Speed { get; set; }
    public double Bearing { get; set; }
    public string Headsign { get; set; }
}