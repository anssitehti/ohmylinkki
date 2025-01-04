using System.ComponentModel.DataAnnotations;

namespace Api.Linkki;

public class LinkkiImportOptions
{
    [Required] public required long ImportInterval { get; init; } = 5000;
    [Required]  public required string WalttiBaseUrl { get; set; }
    
    [Required] public required string WalttiPassword { get; set; }
    [Required] public required string WalttiUsername { get; set; }
}