using System.ComponentModel.DataAnnotations;

namespace Api;

public class OpenAiOptions
{
    [Required]
    public required string Endpoint { get; init; }

    [Required] public required string DeploymentName { get; init; } = "gpt4oMini";
}