using System.ComponentModel.DataAnnotations;

namespace Api;

public class CosmosDbOptions
{
    [Required]
    public required string Endpoint { get; init; } 
}


