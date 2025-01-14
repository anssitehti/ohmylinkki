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
    private readonly Container _locationContainer;
    private readonly Container _routeContainer;

    public LinkkiPlugin(CosmosClient client, IOptions<LinkkiOptions> options)
    {
        _locationContainer = client.GetContainer(options.Value.Database, options.Value.LocationContainer);
        _routeContainer = client.GetContainer(options.Value.Database, options.Value.RouteContainer);
    }

    [KernelFunction("get_locations")]
    [Description("Gets a current location of linkki by line. Line is required.")]
    [return:
        Description(
            "The current location of the line. It can return multiple locations because there can be multiple buses on the same line but in different locations and heading to different destinations.")]
    public async Task<List<LinkkiLocationDetails>> GetLocationsAsync(string line)
    {
        var query = _locationContainer.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Line.Name.ToLower() == line.ToLower());

        var details = new List<LinkkiLocationDetails>();
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                details.Add(new LinkkiLocationDetails
                {
                    Location = item.Location,
                    Speed = item.Vehicle.Speed,
                    Bearing = item.Vehicle.Bearing,
                    Headsign = item.Vehicle.Headsign
                });
            }
        }

        return details;
    }

    [KernelFunction("get_route")]
    [Description("Gets the route of the line by the line name.")]
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


    [KernelFunction("get_available_lines")]
    [Description("Gets the available lines.")]
    [return: Description("Returns the available lines.")]
    public async Task<string[]> GetAvailableLines()
    {
        var query = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
            .Select(l => l.LineName);
        using var iterator = query.ToFeedIterator();
        var lines = new List<string>();
        while (iterator.HasMoreResults)
        {
            lines.AddRange(await iterator.ReadNextAsync());
        }

        Console.WriteLine(lines.ToArray());
        return lines.ToArray();
    }
}

public class LinkkiLocationDetails
{
    public GeoJson Location { get; set; }
    public double Speed { get; set; }
    public double Bearing { get; set; }
    public string Headsign { get; set; }
}