using System.ComponentModel.DataAnnotations;

namespace FunctionApp1.HealthChecks;

public sealed class HealthCheckOptions
{
    public const string SectionName = "HealthCheck";

    public string Schedule { get; init; } = "0 */1 * * * *";

    [Required]
    [Url]
    public string TargetUrl { get; init; } = "https://aspire.dev/";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 5;
}
