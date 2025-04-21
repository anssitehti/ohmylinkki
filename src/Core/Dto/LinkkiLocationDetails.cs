using System.Text.Json.Serialization;

namespace Core.Dto;

public class LinkkiLocationDetails
{
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("speed")] public double Speed { get; set; }
    [JsonPropertyName("bearing")] public double Bearing { get; set; }
    [JsonPropertyName("headsign")] public required string Headsign { get; set; }
    [JsonPropertyName("tripId")] public required string TripId { get; set; }
    [JsonPropertyName("direction")] public uint Direction { get; set; }
    [JsonPropertyName("lineName")] public required string LineName { get; set; }
    [JsonPropertyName("licensePlate")] public required string LicensePlate { get; set; }
    [JsonPropertyName("id")] public required string Id { get; set; }
}