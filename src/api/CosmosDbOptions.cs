using System.ComponentModel.DataAnnotations;

namespace Api;

public class CosmosDbOptions
{
    [Required]
    public required string ConnectionString { get; init; } 
    
    [Required]
    public required string Database { get; init; }
    
    [Required]
    public required string Container { get; init; }
}


