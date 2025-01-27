using System.ComponentModel.DataAnnotations;

namespace Api.AI;

public class OpenAiOptions
{
    [Required] public required string Endpoint { get; init; }

    [Required] public required string DeploymentName { get; init; } = "gpt4oMini";
   
    public int ChatHistoryExpirationMinutes { get; set; } = 5;
}