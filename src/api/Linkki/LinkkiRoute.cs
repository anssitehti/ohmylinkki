using Newtonsoft.Json;

namespace Api.Linkki;

public class LinkkiRoute
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("lineName")] public required string LineName { get; set; }
    [JsonProperty("route")] public required string[] Route { get; set; }
}