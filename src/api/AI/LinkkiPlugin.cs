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
    [Description("Gets the current location and status of buses operating on the specified line number.")]
    [return:
        Description(
            "Returns detailed information about buses currently operating on the specified line, including position, speed, direction, and destination.")]
    private async Task<List<LinkkiLocationDetails>> GetLocationAsync(
        [Description("The bus line number or name to search for.")]
        string lineName)
    {
        var query = _locationContainer.GetItemLinqQueryable<LinkkiLocation>()
            .Where(l => l.Type == "bus" && l.Line.Name.ToLower() == lineName.ToLower().Trim());

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
    
    [KernelFunction("filter_bus_lines_on_map")]
    [Description(
        "Filters the bus lines shown on the map.")]
    private async Task FilterBusLinesOnMapAsync(
        [Description("The bus line names to filter on the map")]
        List<string> lineNames)
    {
        try
        {
            var userId = _httpContextAccessor.HttpContext?.Items["userId"] as string;
            await _webPubSubServiceClient.SendToUserAsync(userId,
                RequestContent.Create(new WebSocketEvent()
                {
                    Type = "filter-bus-lines",
                    Data = lineNames
                }), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
    }
    

    [KernelFunction("show_bus_stop_on_map")]
    [Description(
        "Shows a fixed bus stop location on the map. This is only for displaying stationary bus stops, not for tracking moving bus vehicles.")]
    private async Task ShowBusStopOnMapAsync(
        [Description("The name of the bus stop (not a bus or vehicle)")]
        string busStopName,
        [Description("The longitude coordinate of the fixed bus stop")]
        double longitude,
        [Description("The latitude coordinate of the fixed bus stop")]
        double latitude)
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
                    Type = "show-bus-stop",
                    Data = new
                    {
                        name = busStopName,
                        coordinates = new[] { longitude, latitude }
                    }
                }), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
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


    [KernelFunction("get_bus_stop_details_by_name")]
    [Description(
        "Gets detailed information about a bus stop including its location.")]
    [return:
        Description(
            "Returns complete details about the bus stop including coordinates, distance from user or bus.")]
    private async Task<BusStopLocationDetails?> GetBusStopDetailsByNameAsync(
        [Description("The name of the bus stop to search for")]
        string busStopName,
        [Description("User's or bus line current longitude (0 if unavailable)")]
        double longitude = 0,
        [Description("User's or bus line  current latitude (0 if unavailable)")]
        double latitude = 0)
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

    [KernelFunction("get_bus_arrival_times")]
    [Description("Gets upcoming arrival times for buses at a specific stop and line name.")]
    [return:
        Description(
            "Returns a list of upcoming bus arrivals with line name, trip, stop name, arrival time and minutes until arrival.")]
    public List<BusArrival> GetBusArrivalTimes(
        [Description("Name of the bus stop")] string busStopName,
        [Description("Line name")] string lineName)
    {
        var arrivals = new List<BusArrival>();
        var route = _routeContainer
            .GetItemLinqQueryable<LinkkiRoute>()
            .Where(l => l.LineName.ToLower().Trim() == lineName.ToLower().Trim())
            .FirstOrDefault();
        if (route == null)
        {
            return arrivals;
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki"));
        var nowTime = now.TimeOfDay;

        foreach (var busStop in route.BusStops)
        {
            if (!IsValidTripForCurrentDate(now, busStop.TripId))
            {
                continue;
            }

            var matchingStops = busStop.BusStopDetails
                .Where(sd => sd.Name.Equals(busStopName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var stop in matchingStops.Where(stop => !string.IsNullOrEmpty(stop.ArrivalTime)))
            {
                try
                {
                    var arrivalTime = TimeSpan.Parse(stop.ArrivalTime);

                    if (arrivalTime > (nowTime - TimeSpan.FromMinutes(2)) &&
                        arrivalTime < (nowTime + TimeSpan.FromHours(2)))
                    {
                        var minutesUntil = (int)Math.Round((arrivalTime - nowTime).TotalMinutes);

                        arrivals.Add(new BusArrival
                        {
                            LineName = route.LineName,
                            TripId = busStop.TripId,
                            BusStopName = stop.Name,
                            ArrivalTime = arrivalTime.ToString(),
                            MinutesUntilArrival = minutesUntil,
                        });
                    }
                }
                catch (Exception)
                {
                    // ignore 25:00:00 and other invalid times
                }
            }
        }

        return arrivals.OrderBy(a => a.MinutesUntilArrival).ToList();
    }
    
    private static bool IsValidTripForCurrentDate(DateTime currentDate, string busStopTripId)
    {
        return currentDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => busStopTripId.StartsWith("L_"),
            DayOfWeek.Sunday => busStopTripId.StartsWith("S_"),
            _ => busStopTripId.StartsWith("M-P_")
        };
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
    [JsonPropertyName("id")] public required string Id { get; set; }
}

public class BusStopLocationDetails
{
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("location")] public required Point Location { init; get; }
    [JsonPropertyName("longitude")] public double Longitude => Location.Position.Longitude;
    [JsonPropertyName("latitude")] public double Latitude => Location.Position.Latitude;
    [JsonPropertyName("distance")] public double? Distance { get; set; }
}

public class BusArrival
{
    [JsonPropertyName("lineName")] public required string LineName { get; set; }
    [JsonPropertyName("busStopName")] public required string BusStopName { get; set; }
    [JsonPropertyName("arrivalTime")] public required string ArrivalTime { get; set; }

    [JsonPropertyName("minutesUntilArrival")]
    public int MinutesUntilArrival { get; set; }

    [JsonPropertyName("tripId")] public required string TripId { get; set; }
}