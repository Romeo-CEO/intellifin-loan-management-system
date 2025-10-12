using System.ComponentModel.DataAnnotations;

namespace IntelliFin.UserMigration.Options;

public sealed class MigrationOptions
{
    /// <summary>
    /// Number of users processed per batch when importing into Keycloak.
    /// </summary>
    [Range(1, 1000)]
    public int UserBatchSize { get; set; } = 100;

    /// <summary>
    /// Percentage of the total users to validate as part of the post migration sampling process.
    /// </summary>
    [Range(1, 100)]
    public int ValidationSamplePercentage { get; set; } = 10;

    /// <summary>
    /// Maximum number of retries for Keycloak Admin API calls.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retries, expressed in milliseconds.
    /// </summary>
    [Range(100, 30000)]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Optional override for the output path of migration reports. When null, the current directory is used.
    /// </summary>
    public string? ReportsDirectory { get; set; }
}
