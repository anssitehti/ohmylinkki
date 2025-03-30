using Microsoft.Azure.Cosmos.Spatial;
using Newtonsoft.Json;

namespace Api.Linkki;

public class LinkkiLocation
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("timestamp")] public required DateTimeOffset Timestamp { get; set; }
    [JsonProperty("location")] public required Point Location { get; set; }
    [JsonProperty("vehicle")] public required Vehicle Vehicle { get; set; }
    [JsonProperty("line")] public required Line Line { get; set; }

    [JsonProperty("type")] public string Type { get; set; } = "bus";
    
    [JsonProperty(PropertyName = "ttl")] public int TimeToLive { get; set; } = 3600;
}

public class Vehicle
{
    [JsonProperty("licensePlate")] public required string LicensePlate { get; set; }
    [JsonProperty("headsign")] public required string Headsign { get; set; }
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("speed")] public float Speed { get; set; }
    [JsonProperty("bearing")] public float Bearing { get; set; }
}

public class Line
{
    [JsonProperty("name")] public required string Name { get; set; }
    [JsonProperty("routeId")] public required string RouteId { get; set; }
    [JsonProperty("direction")] public required uint Direction { get; set; }
    [JsonProperty("tripId")] public required string TripId { get; set; }
}