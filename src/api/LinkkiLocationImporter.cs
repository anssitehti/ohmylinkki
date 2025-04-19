using Azure.Core;
using Core;
using Core.Services;
using Microsoft.Azure.Cosmos.Spatial;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.Options;
using RestSharp;
using RestSharp.Authenticators;
using TransitRealtime;
using ContentType = Azure.Core.ContentType;
using Line = Core.Services.Line;
using LinkkiLocation = Core.Services.LinkkiLocation;
using Vehicle = Core.Services.Vehicle;

namespace Api;

public class LinkkiLocationImporter : BackgroundService
{
    private readonly ILogger<LinkkiLocationImporter> _logger;
    private readonly RestClient _client;
    private readonly LinkkiOptions _options;
    private readonly WebPubSubServiceClient<LinkkiHub> _webPubSubServiceClient;
    private readonly LinkkiService _linkkiService;

    public LinkkiLocationImporter(ILogger<LinkkiLocationImporter> logger, IOptions<LinkkiOptions> linkkiOptions,
        LinkkiService linkkiService,
        WebPubSubServiceClient<LinkkiHub> webPubSubServiceClient)
    {
        _logger = logger;
        _options = linkkiOptions.Value;
        _linkkiService = linkkiService;
        var restClientOptions = new RestClientOptions(_options.WalttiBaseUrl)
        {
            Authenticator = new HttpBasicAuthenticator(_options.WalttiUsername, _options.WalttiPassword)
        };
        _client = new RestClient(restClientOptions);
        _webPubSubServiceClient = webPubSubServiceClient;
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
                var lineName = await _linkkiService.GetLineName(feedEntity.Vehicle.Trip.RouteId);
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


        await _linkkiService.UpsertLocationsAsync(locations.Values, cancellationToken);

        await PublishToAllAsync(locations.Values);
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


    private async Task PublishToAllAsync(IEnumerable<LinkkiLocation> locations)
    {
        try
        {
            await _webPubSubServiceClient.SendToAllAsync(
                RequestContent.Create(new WebSocketEvent()
                {
                    Type = "linkki-location",
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