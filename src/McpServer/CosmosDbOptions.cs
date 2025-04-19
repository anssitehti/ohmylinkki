using System.ComponentModel.DataAnnotations;

namespace Api;

public class CosmosDbOptions
{
    [Required] public required string Endpoint { get; init; }

    [Required] public required string Database { get; init; } = "linkki";

    [Required] public required string LocationContainer { get; init; } = "locations";

    [Required] public required string RouteContainer { get; init; } = "routes";
}