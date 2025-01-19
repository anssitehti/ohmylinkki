using Microsoft.Azure.Cosmos.Spatial;
using Newtonsoft.Json;

namespace Api.Linkki;

public class BusStopLocation
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("stopId")] public required string StopId { get; set; }
    [JsonProperty("name")] public required string Name { get; set; }
    [JsonProperty("location")] public required Point Location { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = "stop";
}