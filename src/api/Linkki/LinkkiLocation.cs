using Newtonsoft.Json;

namespace Api.Linkki;

public class LinkkiLocation
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("timestamp")] public required DateTimeOffset Timestamp { get; set; }
    [JsonProperty("location")] public required GeoJson Location { get; set; }
    [JsonProperty("vehicle")] public required Vehicle Vehicle { get; set; }
    [JsonProperty("line")] public required Line Line { get; set; }
}

public class Vehicle
{
    [JsonProperty("licensePlate")] public required string LicensePlate { get; set; }
    [JsonProperty("headsign")] public required string Headsign { get; set; }
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("speed")] public float Speed { get; set; }
    [JsonProperty("bearing")] public float Bearing { get; set; }
}

public class GeoJson
{
    [JsonProperty("type")] public required string Type { get; set; }
    [JsonProperty("coordinates")] public required float[] Coordinates { get; set; }
}

public class Line
{
    [JsonProperty("name")] public required string Name { get; set; }
    [JsonProperty("routeId")] public required string RouteId { get; set; }
    [JsonProperty("direction")] public required uint Direction { get; set; }
}