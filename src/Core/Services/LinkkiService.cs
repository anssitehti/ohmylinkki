using Core.Dto;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;


namespace Core.Services;

public class LinkkiService
{
    private readonly ILogger<LinkkiService> _logger;
    private readonly Container _locationContainer;
    private readonly Container _routeContainer;
    private readonly IMemoryCache _memoryCache;

    public LinkkiService(string database, string locationContainer, string routeContainer, CosmosClient cosmosClient,
        IMemoryCache memoryCache, ILogger<LinkkiService> logger)
    {
        _logger = logger;
        _locationContainer =
            cosmosClient.GetContainer(database, locationContainer);
        _routeContainer = cosmosClient.GetContainer(database, routeContainer);
        _memoryCache = memoryCache;
    }

    public async Task UpsertLocationsAsync(IEnumerable<LinkkiLocation> locations, CancellationToken cancellationToken)
    {
        List<Task> upsertItemTasks = [];
        foreach (var location in locations)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    await _locationContainer.UpsertItemAsync(location, new PartitionKey(location.Type),
                        cancellationToken: cancellationToken);
                }
                catch (CosmosException ex)
                {
                    _logger.LogError(ex, "Failed to upsert location of {Line}", location.Line.Name);
                }
            }, cancellationToken);
            upsertItemTasks.Add(task);
        }

        await Task.WhenAll(upsertItemTasks);
    }

    public async Task<string?> GetLineName(string routeId)
    {
        return await _memoryCache.GetOrCreateAsync(routeId, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);
            var lineName = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
                .Where(x => x.Id == routeId).Select(x => x.LineName).FirstOrDefault();
            return Task.FromResult(lineName);
        });
    }

    public async Task<List<LinkkiLocationDetails>> GetLocationAsync(
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

    public async Task<List<BusStopLocationDetails>> GetClosestBusStopAsync(double longitude, double latitude,
        double distance)
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

    public List<string>? GetBusStops(string lineName, string tripId)
    {
        var route = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
            .Where(l => l.LineName.ToLower().Trim() == lineName.ToLower().Trim())
            .FirstOrDefault();

        return route?.BusStops
            .Where(busStop => busStop.TripId == tripId)
            .SelectMany(busStop => busStop.BusStopDetails)
            .Select(x => x.Name).ToList();
    }

    public async Task<string[]> GetAvailableLinesAsync()
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

    public async Task<BusStopLocationDetails?> GetBusStopDetailsByNameAsync(
        string busStopName,
        double longitude,
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

    public List<BusArrival> GetBusArrivalTimes(string busStopName, string lineName)
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