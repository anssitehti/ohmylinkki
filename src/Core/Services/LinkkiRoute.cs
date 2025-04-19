using Newtonsoft.Json;

namespace Core.Services;

public class LinkkiRoute
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("lineName")] public required string LineName { get; set; }

    [JsonProperty("busStops")] public required List<TripBusStop> BusStops { get; set; }
}

public class TripBusStop
{
    [JsonProperty("tripId")] public required string TripId { get; set; }
    [JsonProperty("stops")] public required List<BusStopDetails> BusStopDetails { get; set; }
}

public class BusStopDetails
{
    [JsonProperty("id")] public required string Id { get; set; }
    [JsonProperty("name")] public required string Name { get; set; }
    [JsonProperty("arrivalTime")] public required string ArrivalTime { get; set; }
}