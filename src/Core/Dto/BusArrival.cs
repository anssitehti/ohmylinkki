using System.Text.Json.Serialization;

namespace Core.Dto;

public class BusArrival
{
    [JsonPropertyName("lineName")] public required string LineName { get; set; }
    [JsonPropertyName("busStopName")] public required string BusStopName { get; set; }
    [JsonPropertyName("arrivalTime")] public required string ArrivalTime { get; set; }

    [JsonPropertyName("minutesUntilArrival")]
    public int MinutesUntilArrival { get; set; }

    [JsonPropertyName("tripId")] public required string TripId { get; set; }
}