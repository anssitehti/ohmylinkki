using System.ComponentModel;
using Api.Linkki;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Api.AI;

public class LinkkiPlugin
{
    private readonly Container _locationContainer;
    private readonly Container _routeContainer;
    private readonly WebPubSubServiceClient<LinkkiHub> _webPubSubServiceClient;
    private readonly ILogger<LinkkiPlugin> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LinkkiPlugin(CosmosClient client, IOptions<LinkkiOptions> options,
        WebPubSubServiceClient<LinkkiHub> webPubSubServiceClient, ILogger<LinkkiPlugin> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _locationContainer = client.GetContainer(options.Value.Database, options.Value.LocationContainer);
        _routeContainer = client.GetContainer(options.Value.Database, options.Value.RouteContainer);
        _webPubSubServiceClient = webPubSubServiceClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    [KernelFunction("get_location")]
    [Description("Gets the current location of the bus on the given line.")]
    [return:
        Description("Returns the locations of bus that are currently on the given line. Each bus are represented by their own trip. Trip and line are unique identifiers.")]
    private async Task<List<LinkkiLocationDetails>> GetLocationAsync(string line)
    {
        var query = _locationContainer.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Line.Name.ToLower() == line.ToLower().Trim() && l.Type == "bus");

        var details = new List<LinkkiLocationDetails>();
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                details.Add(new LinkkiLocationDetails
                {
                    Longitude = item.Location.Position.Longitude,
                    Latitude = item.Location.Position.Latitude,
                    Speed = item.Vehicle.Speed,
                    Bearing = item.Vehicle.Bearing,
                    Headsign = item.Vehicle.Headsign,
                    TripId = item.Line.TripId,
                    Direction = item.Line.Direction,
                    LineName = item.Line.Name
                });
            }
        }

        return details;
    }
    
    [KernelFunction("get_bus_stops_by_line_and_trip")]
    [Description("Gets the bus stops where the bus stops for the given line and trip. To use this you need to first get location of the bus using the get_location.")]
    [return: Description("Returns the bus stops.")]
    private List<BusStopDetails> GetBusStopsByLineAndTrip(string line, string tripId)
    {
        var route = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
            .Where(l => l.LineName.ToLower().Trim() == line.ToLower().Trim())
            .FirstOrDefault();
        
        var busStopDetails = route?.BusStops
            .Where(busStop => busStop.TripId == tripId)
            .Select(busStop => busStop.BusStopDetails)
            .FirstOrDefault();

        return busStopDetails ?? [];
    }
    
    [KernelFunction("get_available_lines")]
    [Description("Gets the available lines.")]
    [return: Description("Returns the available lines.")]
    private async Task<string[]> GetAvailableLines()
    {
        var query = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
            .Select(l => l.LineName);
        using var iterator = query.ToFeedIterator();
        var lines = new List<string>();
        while (iterator.HasMoreResults)
        {
            lines.AddRange(await iterator.ReadNextAsync());
        }

        return lines.ToArray();
    }

    [KernelFunction("get_closest_bus_stops")]
    [Description(
        "Gets the closest bus stops to the given location and within the given distance. The default distance is 300 meters.")]
    [return: Description("Returns the bus stops.")]
    private async Task<List<BusStopLocationDetails>> GetClosestBusStopAsync(double longitude, double latitude,
        double distance = 300)
    {
        var query = _locationContainer.GetItemLinqQueryable<BusStopLocation>()
            .Where(s => s.Type == "stop")
            .Where(s => s.Location.Distance(new Point(longitude, latitude)) < distance)
            .Select(s => new BusStopLocationDetails
            {
                Name = s.Name,
                Location = s.Location,
                Distance = s.Location.Distance(new Point(longitude, latitude))
            });
        using var iterator = query.ToFeedIterator();
        var busStops = new List<BusStopLocationDetails>();
        while (iterator.HasMoreResults)
        {
            busStops.AddRange(await iterator.ReadNextAsync());
        }

        return busStops;
    }

    [KernelFunction("show_bus_stop_location_on_map")]
    [Description("Shows the bus stop location on map. Bus stop name, longitude, and latitude are required.")]
    private async Task ShowBusStopLocationOnMapAsync(string? name, double longitude, double latitude)
    {
        try
        {
            var userId = _httpContextAccessor.HttpContext?.Items["userId"] as string;
            await _webPubSubServiceClient.SendToUserAsync(userId,
                RequestContent.Create(new WebSocketEvent()
                {
                    Type = "stop",
                    Data = new
                    {
                        name = name ?? "unknown",
                        coordinates = new[] { longitude, latitude }
                    }
                }), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
    }
}

public class LinkkiLocationDetails
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public double Speed { get; set; }
    public double Bearing { get; set; }
    public string Headsign { get; set; }
    public string TripId { get; set; }
    public uint Direction { get; set; }
    public string LineName { get; set; }
}

public class BusStopLocationDetails
{
    public required string Name { get; set; }
    public required Point Location { set; get; }
    public double Longitude => Location.Position.Longitude;
    public double Latitude => Location.Position.Latitude;
    public required double Distance { get; set; }
}