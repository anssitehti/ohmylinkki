using System.Text.Json.Serialization;

namespace Api.Linkki;

internal class WebSocketEvent
{
    [JsonPropertyName("type")]  public required string Type { get; set; }
    [JsonPropertyName("data")]  public required object Data { get; set; }
}