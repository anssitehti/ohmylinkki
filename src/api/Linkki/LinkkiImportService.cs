using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Extensions.Options;
using RestSharp;
using RestSharp.Authenticators;
using TransitRealtime;
using ContentType = Azure.Core.ContentType;

namespace Api.Linkki;

public class LinkkiImportService : BackgroundService
{
    private readonly ILogger<LinkkiImportService> _logger;
    private readonly RestClient _client;
    private readonly LinkkiImportOptions _options;
    private readonly Container _container;
    private readonly WebPubSubServiceClient<LinkkiHub> _webPubSubServiceClient;

    private static readonly Dictionary<string, string> RouteToLine = new()
    {
        { "905", "S5" },
        { "9221", "22" },
        { "901", "S1" },
        { "9411", "41" },
        { "9361", "36" },
        { "9071", "7" },
        { "9231", "23" },
        { "906", "S6" },
        { "903", "S3" },
        { "902", "S2" },
        { "9092", "9K" },
        { "9121", "12" },
        { "9211", "21" },
        { "9031", "3" },
        { "9143", "143" },
        { "9201", "20" },
        { "9151", "15" },
        { "9161", "16" },
        { "904", "S4" },
        { "9461", "46" },
        { "6140", "14" },
        { "9451", "45" },
        { "12", "141" },
        { "6141", "14" }
    };

    public LinkkiImportService(ILogger<LinkkiImportService> logger, IOptions<LinkkiImportOptions> linkkiOptions,
        CosmosClient client, IOptions<CosmosDbOptions> cosmosDbOptions,
        WebPubSubServiceClient<LinkkiHub> webPubSubServiceClient)
    {
        _logger = logger;
        _options = linkkiOptions.Value;
        var restClientOptions = new RestClientOptions(_options.WalttiBaseUrl)
        {
            Authenticator = new HttpBasicAuthenticator(_options.WalttiUsername, _options.WalttiPassword)
        };
        _client = new RestClient(restClientOptions);
        var database = client.GetDatabase(cosmosDbOptions.Value.Database);
        _container = database.GetContainer(cosmosDbOptions.Value.Container);
        _webPubSubServiceClient = webPubSubServiceClient;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(_options.ImportInterval));
        while (await timer.WaitForNextTickAsync(cancellationToken)) await ImportAsync(cancellationToken);
    }

    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing Linkki locations...");
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

        var linkkiLocations = new List<LinkkiLocation>();
        if (response.RawBytes != null)
        {
            using var stream = new MemoryStream(response.RawBytes);
            var feedMessage = FeedMessage.Parser.ParseFrom(stream);
            foreach (var feedEntity in feedMessage.Entity)
            {
                var validRouteId = RouteToLine.ContainsKey(feedEntity.Vehicle.Trip.RouteId);
                if (!validRouteId)
                {
                    _logger.LogWarning("Unknown route id {RouteId} {Headsign}", feedEntity.Vehicle.Trip.RouteId,
                        feedEntity.Vehicle.Vehicle.Label);
                    continue;
                }

                var linkki = new LinkkiLocation()
                {
                    Id = Guid.NewGuid().ToString(),
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
                        Name = RouteToLine[feedEntity.Vehicle.Trip.RouteId],
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
                linkkiLocations.Add(linkki);
            }
        }
        
        var lineGroups = linkkiLocations.GroupBy(x => x.Line.Name);
        var latestLocations = lineGroups.Select(x => x.OrderByDescending(y => y.Timestamp).First()).ToList();
        
        foreach (var location in latestLocations)
        {
            await CreateItemsAsync(location, cancellationToken);
        }

        await PublishAsync(latestLocations, cancellationToken);
    }

    private async Task CreateItemsAsync(LinkkiLocation location, CancellationToken cancellationToken)
    {
        try
        {
            await _container.CreateItemAsync(location, new PartitionKey(location.Line.Name),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to save location of {Line}", location.Line.Name);
        }
    }

    private async Task PublishAsync(IEnumerable<LinkkiLocation> locations, CancellationToken cancellationToken)
    {
        try
        {
            await _webPubSubServiceClient.SendToAllAsync(
                RequestContent.Create(locations.Select(location =>
                    new { line = location.Line.Name, location = location.Location })), ContentType.ApplicationJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish locations to WebPubSub Hub.");
        }
    }
}