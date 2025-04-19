using System.Text.Json.Serialization;
using Azure.Core;
using Microsoft.Azure.WebPubSub.AspNetCore;

namespace Api;

public class LinkkiHub : WebPubSubHub;

public class LinkkiHubService
{
    private readonly WebPubSubServiceClient<LinkkiHub> _webPubSubServiceClient;
    private readonly ILogger<LinkkiHubService> _logger;

    public LinkkiHubService(WebPubSubServiceClient<LinkkiHub> webPubSubServiceClient, ILogger<LinkkiHubService> logger)
    {
        _webPubSubServiceClient = webPubSubServiceClient;
        _logger = logger;
    }

    public async Task FilterBusLinesOnMapAsync(string userId, List<string> lineNames)
    {
        try
        {
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

    public async Task ShowBusStopOnMapAsync(string userId, string busStopName, double longitude, double latitude)
    {
        if (longitude == 0 || latitude == 0)
        {
            return;
        }

        try
        {
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
}

public class WebSocketEvent
{
    [JsonPropertyName("type")]  public required string Type { get; set; }
    [JsonPropertyName("data")]  public required object Data { get; set; }
}