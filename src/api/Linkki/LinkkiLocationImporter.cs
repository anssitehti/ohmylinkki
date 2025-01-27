using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RestSharp;
using RestSharp.Authenticators;
using TransitRealtime;
using ContentType = Azure.Core.ContentType;

namespace Api.Linkki;

public class LinkkiLocationImporter : BackgroundService
{
    private readonly ILogger<LinkkiLocationImporter> _logger;
    private readonly RestClient _client;
    private readonly LinkkiOptions _options;
    private readonly Container _locationContainer;
    private readonly WebPubSubServiceClient<LinkkiHub> _webPubSubServiceClient;
    private readonly Container _routeContainer;
    private readonly IMemoryCache _memoryCache;

    public LinkkiLocationImporter(ILogger<LinkkiLocationImporter> logger, IOptions<LinkkiOptions> linkkiOptions,
        CosmosClient cosmosClient,
        WebPubSubServiceClient<LinkkiHub> webPubSubServiceClient, IMemoryCache memoryCache)
    {
        _logger = logger;
        _options = linkkiOptions.Value;
        var restClientOptions = new RestClientOptions(_options.WalttiBaseUrl)
        {
            Authenticator = new HttpBasicAuthenticator(_options.WalttiUsername, _options.WalttiPassword)
        };
        _client = new RestClient(restClientOptions);
        _locationContainer =
            cosmosClient.GetContainer(linkkiOptions.Value.Database, linkkiOptions.Value.LocationContainer);
        _routeContainer = cosmosClient.GetContainer(linkkiOptions.Value.Database, linkkiOptions.Value.RouteContainer);
        _webPubSubServiceClient = webPubSubServiceClient;
        _memoryCache = memoryCache;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(_options.ImportInterval));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await ImportAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import locations.");
            }
        }
    }

    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var request = new RestRequest("vehicleposition");
        request.AddHeader("Accept", "application/x-protobuf");
        var response = await _client.ExecuteAsync(request, cancellationToken);
        if (!response.IsSuccessful && response.StatusCode > 0)
        {
            _logger.LogError("Failed to fetch locations: {StatusCode} {StatusDescription} {ResponseUri}",
                response.StatusCode,
                response.StatusDescription, response.ResponseUri);
            return;
        }

        var locations = new Dictionary<string, LinkkiLocation>();
        if (response.RawBytes != null)
        {
            using var stream = new MemoryStream(response.RawBytes);
            var feedMessage = FeedMessage.Parser.ParseFrom(stream);
            foreach (var feedEntity in feedMessage.Entity)
            {
                var id = feedEntity.Vehicle.Vehicle.Id;
                var lineName = await GetLineName(feedEntity.Vehicle.Trip.RouteId);
                if (lineName == null)
                {
                    _logger.LogWarning("Unknown route id {RouteId} {Headsign}", feedEntity.Vehicle.Trip.RouteId,
                        feedEntity.Vehicle.Vehicle.Label);
                    continue;
                }

                if (locations.TryGetValue(id, out var existingLocation))
                {
                    if (DateTimeOffset.FromUnixTimeSeconds((long)feedEntity.Vehicle.Timestamp) >
                        existingLocation.Timestamp)
                    {
                        locations[id] = MapLinkkiLocation(feedEntity, lineName);
                    }
                }
                else
                {
                    locations.Add(id, MapLinkkiLocation(feedEntity, lineName));
                }
            }
        }


        await UpsertLocationsAsync(locations.Values, cancellationToken);

        await PublishToAllAsync(locations.Values);
    }

    private async Task<string?> GetLineName(string routeId)
    {
        return await _memoryCache.GetOrCreateAsync(routeId, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);
            var lineName = _routeContainer.GetItemLinqQueryable<LinkkiRoute>()
                .Where(x => x.Id == routeId).Select(x => x.LineName).FirstOrDefault();
            return Task.FromResult(lineName);
        });
    }

    private LinkkiLocation MapLinkkiLocation(FeedEntity feedEntity, string lineName)
    {
        var location = new LinkkiLocation()
        {
            Id = feedEntity.Vehicle.Vehicle.Id,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)feedEntity.Vehicle.Timestamp),
            Location = new Point(feedEntity.Vehicle.Position.Longitude, feedEntity.Vehicle.Position.Latitude),
            Line = new Line
            {
                Name = lineName,
                RouteId = feedEntity.Vehicle.Trip.RouteId,
                Direction = feedEntity.Vehicle.Trip.DirectionId,
                TripId = feedEntity.Vehicle.Trip.TripId
            },
            Vehicle = new Vehicle
            {
                Id = feedEntity.Vehicle.Vehicle.Id,
                LicensePlate = feedEntity.Vehicle.Vehicle.LicensePlate,
                Headsign = feedEntity.Vehicle.Vehicle.Label,
                Speed = feedEntity.Vehicle.Position.Speed,
                Bearing = feedEntity.Vehicle.Position.Bearing
            }
        };
        return location;
    }

    private async Task UpsertLocationsAsync(IEnumerable<LinkkiLocation> locations, CancellationToken cancellationToken)
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

    private async Task PublishToAllAsync(IEnumerable<LinkkiLocation> locations)
    {
        try
        {
            await _webPubSubServiceClient.SendToAllAsync(
                RequestContent.Create(new WebSocketEvent()
                {
                    Type = "bus",
                    Data = locations.Select(location =>
                        new
                        {
                            id = location.Id,
                            line = location.Line.Name,
                            coordinates = location.Location.Position.Coordinates,
                            bearing = location.Vehicle.Bearing,
                        })
                }), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
    }
}