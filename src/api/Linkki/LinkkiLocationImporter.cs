using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebPubSub.AspNetCore;
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
    private readonly Container _container;
    private readonly WebPubSubServiceClient<LinkkiHub> _webPubSubServiceClient;

    private static readonly Dictionary<string, string> RouteToLine = new()
    {
        { "3", "Uuraisten Liikenne" },
        { "12", "Matka Mäkelä" },
        { "13", "Matka Mäkelä" },
        { "14", "Matka Mäkelä" },
        { "15", "Matka Mäkelä" },
        { "16", "Matka Mäkelä" },
        { "102", "Peuran Liikenne" },
        { "118", "Pohjolan Matka" },
        { "411", "Koivuranta" },
        { "412", "Koivuranta" },
        { "901", "S1" },
        { "902", "S2" },
        { "903", "S3" },
        { "904", "S4" },
        { "905", "S5" },
        { "906", "S6" },
        { "6140", "140" },
        { "6141", "141" },
        { "6671", "Koiviston Auto" },
        { "6701", "Kouluvuoro" },
        { "6702", "Kouluvuoro" },
        { "6711", "Kouluvuoro" },
        { "6712", "Kouluvuoro" },
        { "6832", "Kouluvuoro" },
        { "6901", "kouluv Äki" },
        { "6902", "Kouluv Äki" },
        { "6908", "Mämmensalmi" },
        { "6911", "Kouluvuoro" },
        { "6912", "Kouluvuoro" },
        { "6913", "Kouluvuoro" },
        { "9017", "1M" },
        { "9027", "2V" },
        { "9028", "S2M" },
        { "9031", "3" },
        { "9033", "3K" },
        { "9043", "4K" },
        { "9051", "5" },
        { "9053", "5K" },
        { "9071", "7" },
        { "9081", "8" },
        { "9091", "9" },
        { "9092", "9K" },
        { "9121", "12" },
        { "9123", "12K" },
        { "9131", "13" },
        { "9141", "14" },
        { "9143", "14M" },
        { "9151", "15" },
        { "9153", "15K" },
    };

    public LinkkiLocationImporter(ILogger<LinkkiLocationImporter> logger, IOptions<LinkkiOptions> linkkiOptions,
        CosmosClient cosmosClient,
        WebPubSubServiceClient<LinkkiHub> webPubSubServiceClient)
    {
        _logger = logger;
        _options = linkkiOptions.Value;
        var restClientOptions = new RestClientOptions(_options.WalttiBaseUrl)
        {
            Authenticator = new HttpBasicAuthenticator(_options.WalttiUsername, _options.WalttiPassword)
        };
        _client = new RestClient(restClientOptions);
        _container = cosmosClient.GetContainer(linkkiOptions.Value.Database, linkkiOptions.Value.LocationContainer);
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
                if (!RouteToLine.TryGetValue(feedEntity.Vehicle.Trip.RouteId, out var line))
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
                        locations[id] = MapLinkkiLocation(feedEntity, line);
                    }
                }
                else
                {
                    locations.Add(id, MapLinkkiLocation(feedEntity, line));
                }
            }
        }


        await UpsertLocationsAsync(locations.Values, cancellationToken);

        await PublishToAllAsync(locations.Values, cancellationToken);
    }

    private LinkkiLocation MapLinkkiLocation(FeedEntity feedEntity, string line)
    {
        var location = new LinkkiLocation()
        {
            Id = feedEntity.Vehicle.Vehicle.Id,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)feedEntity.Vehicle.Timestamp),
            Location =
                new GeoJson()
                {
                    Type = "Point",
                    Coordinates =
                    [
                        feedEntity.Vehicle.Position.Longitude, feedEntity.Vehicle.Position.Latitude
                    ]
                },
            Line = new Line
            {
                Name = line,
                RouteId = feedEntity.Vehicle.Trip.RouteId,
                Direction = feedEntity.Vehicle.Trip.DirectionId
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
                    await _container.UpsertItemAsync(location, new PartitionKey(location.Line.Name),
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

    private async Task PublishToAllAsync(IEnumerable<LinkkiLocation> locations, CancellationToken cancellationToken)
    {
        try
        {
            await _webPubSubServiceClient.SendToAllAsync(
                RequestContent.Create(locations.Select(location =>
                    new
                    {
                        id = location.Id,
                        line = location.Line.Name,
                        location = location.Location,
                        bearing = location.Vehicle.Bearing,
                    })), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
    }
}