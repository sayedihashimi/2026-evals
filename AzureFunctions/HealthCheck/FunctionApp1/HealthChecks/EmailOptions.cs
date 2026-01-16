using System.ComponentModel.DataAnnotations;

namespace FunctionApp1.HealthChecks;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string SenderAddress { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string RecipientAddress { get; init; } = "report@example.com";
}
