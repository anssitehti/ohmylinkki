using System.Text.Json.Serialization;

namespace Api.Linkki;

internal class WebSocketEvent
{
    [JsonPropertyName("type")]  public string Type { get; set; }
    [JsonPropertyName("data")]  public object Data { get; set; }
}