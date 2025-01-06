using System.ComponentModel.DataAnnotations;

namespace Api;

public class CosmosDbOptions
{
    [Required]
    public required string ConnectionString { get; init; } 
}


