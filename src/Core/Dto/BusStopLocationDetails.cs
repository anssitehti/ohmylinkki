using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos.Spatial;

namespace Core.Dto;

public class BusStopLocationDetails
{
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("location")] public required Point Location { init; get; }
    [JsonPropertyName("longitude")] public double Longitude => Location.Position.Longitude;
    [JsonPropertyName("latitude")] public double Latitude => Location.Position.Latitude;
    [JsonPropertyName("distance")] public double? Distance { get; set; }
}