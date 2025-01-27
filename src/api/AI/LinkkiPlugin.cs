using System.ComponentModel;
using System.Text.Json.Serialization;
using Api.Linkki;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Container = Microsoft.Azure.Cosmos.Container;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global


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
        Description(
            "Returns the locations of buses that are currently on the given line. Each bus is represented by its own trip. Trip and line are unique identifiers.")]
    private async Task<List<LinkkiLocationDetails>> GetLocationAsync(string lineName)
    {
        var query = _locationContainer.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Line.Name.ToLower() == lineName.ToLower().Trim() && l.Type == "bus");

        var details = new List<LinkkiLocationDetails>();
        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            details.AddRange((await iterator.ReadNextAsync()).Select(item => new LinkkiLocationDetails
            {
                Id = item.Id,
                Longitude = item.Location.Position.Longitude,
                Latitude = item.Location.Position.Latitude,
                Speed = item.Vehicle.Speed,
                Bearing = item.Vehicle.Bearing,
                Headsign = item.Vehicle.Headsign,
                TripId = item.Line.TripId,
                Direction = item.Line.Direction,
                LineName = item.Line.Name,
                LicensePlate = item.Vehicle.LicensePlate
            }));
        }

        return details;
    }

    [KernelFunction("get_bus_stop_names")]
    [Description(
        "Gets the bus stops names for the given line and tripId.")]
    [return: Description("Returns the bus stops names.")]
    private List<string>? GetBusStops(string lineName, string tripId)
    {
        var route = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
            .Where(l => l.LineName.ToLower().Trim() == lineName.ToLower().Trim())
            .FirstOrDefault();

      return route?.BusStops
            .Where(busStop => busStop.TripId == tripId)
            .SelectMany(busStop => busStop.BusStopDetails)
            .Select(x => x.Name).ToList();
      
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
    [Description("Shows the bus stop location on the map. Bus stop name, longitude, and latitude are required.")]
    private async Task ShowBusStopLocationOnMapAsync(string? busStopName, double longitude, double latitude)
    {
        if (longitude == 0 || latitude == 0)
        {
            return;
        }

        try
        {
            var userId = _httpContextAccessor.HttpContext?.Items["userId"] as string;
            await _webPubSubServiceClient.SendToUserAsync(userId,
                RequestContent.Create(new WebSocketEvent()
                {
                    Type = "stop",
                    Data = new
                    {
                        name = busStopName ?? "unknown",
                        coordinates = new[] { longitude, latitude }
                    }
                }), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
    }

    [KernelFunction("get_bus_stop_details_by_name")]
    [Description("Gets the details of a bus stop by its name. Also accepts user location to calculate the distance.")]
    [return: Description("Returns the details of the bus stop that contains name and arrival time of the bus.")]
    private async Task<BusStopLocationDetails?> GetBusStopDetailsByNameAsync(string busStopName, double longitude,
        double latitude)
    {
        var query = _locationContainer.GetItemLinqQueryable<BusStopLocation>()
            .Where(s => s.Type == "stop")
            .Where(s => s.Name.ToLower() == busStopName.ToLower().Trim())
            .Select(s => new BusStopLocationDetails
            {
                Name = s.Name,
                Location = s.Location,
                Distance = (longitude != 0 && latitude != 0)
                    ? s.Location.Distance(new Point(longitude, latitude))
                    : 0
            });

        using var iterator = query.ToFeedIterator();
        var busStop = await iterator.ReadNextAsync();
        return busStop.FirstOrDefault();
    }
    
    [KernelFunction("get_bus_arrival_time")]
    [Description(
        "Gets the time when the bus will be at the specified bus stop for the given line and tripId. Use only this function to get the arrival time.")]
    [return: Description("Returns the arrival time of the next bus at the specified bus stop.")]
    private string? GetNextBusArrivalTimeAsync(string lineName, string tripId, string busStopName)
    {
        var route = _routeContainer
            .GetItemLinqQueryable<LinkkiRoute>()
            .Where(l => l.LineName.ToLower().Trim() == lineName.ToLower().Trim())
            .FirstOrDefault();
        
        var nextArrivalTIme = route?.BusStops.Where(stop =>
                string.Equals(stop.TripId, tripId.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .SelectMany(stop => stop.BusStopDetails)
            .Where(sd => string.Equals(sd.Name, busStopName.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .Select(sd => sd.ArrivalTime)
            .FirstOrDefault();
        
        return nextArrivalTIme;
    }
}

public class LinkkiLocationDetails
{
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("speed")] public double Speed { get; set; }
    [JsonPropertyName("bearing")] public double Bearing { get; set; }
    [JsonPropertyName("headsign")] public required string Headsign { get; set; }
    [JsonPropertyName("tripId")] public required string TripId { get; set; }
    [JsonPropertyName("direction")] public uint Direction { get; set; }
    [JsonPropertyName("lineName")] public required string LineName { get; set; }
    [JsonPropertyName("licensePlate")] public required string LicensePlate { get; set; }
    [JsonPropertyName("id")]  public required string Id { get; set; }
}

public class BusStopLocationDetails
{
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("location")] public required Point Location { init; get; }
    [JsonPropertyName("longitude")] public double Longitude => Location.Position.Longitude;
    [JsonPropertyName("latitude")] public double Latitude => Location.Position.Latitude;
    [JsonPropertyName("distance")] public required double Distance { get; set; }
}