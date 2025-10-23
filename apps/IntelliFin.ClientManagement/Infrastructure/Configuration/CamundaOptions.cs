namespace IntelliFin.ClientManagement.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Camunda/Zeebe integration
/// Controls worker behavior and Zeebe gateway connectivity
/// </summary>
public class CamundaOptions
{
    /// <summary>
    /// Configuration section name for binding
    /// </summary>
    public const string SectionName = "Camunda";

    /// <summary>
    /// Zeebe gateway address (e.g., "http://localhost:26500")
    /// </summary>
    public string GatewayAddress { get; set; } = "http://localhost:26500";

    /// <summary>
    /// Worker name for registration with Zeebe (service identifier)
    /// </summary>
    public string WorkerName { get; set; } = "IntelliFin.ClientManagement";

    /// <summary>
    /// Maximum number of jobs to activate concurrently per worker
    /// Default: 32 (balanced for typical workflow volume)
    /// </summary>
    public int MaxJobsToActivate { get; set; } = 32;

    /// <summary>
    /// Polling interval in seconds for checking new jobs
    /// Default: 5 seconds (balances responsiveness and load)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Request timeout in seconds for Zeebe client operations
    /// Default: 30 seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether Camunda integration is enabled
    /// Set to false to disable workers in environments without Camunda
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for failed jobs before sending to DLQ
    /// Default: 3 (exponential backoff: 1s, 2s, 4s)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// List of topic names this worker service handles
    /// Used for registration and monitoring
    /// </summary>
    public List<string> Topics { get; set; } = new();
}
