using System.ComponentModel.DataAnnotations;

namespace Api.Linkki;

public class LinkkiOptions
{
    [Required] public required long ImportInterval { get; init; } = 2000;

    [Required]
    public required string WalttiBaseUrl { get; set; } = "https://data.waltti.fi/jyvaskyla/api/gtfsrealtime/v1.0/feed";

    [Required] public required string WalttiPassword { get; set; }
    [Required] public required string WalttiUsername { get; set; }

    [Required] public required string Database { get; init; } = "linkki";

    [Required] public required string LocationContainer { get; init; } = "locations";

    [Required] public required string RouteContainer { get; init; } = "routes";
}