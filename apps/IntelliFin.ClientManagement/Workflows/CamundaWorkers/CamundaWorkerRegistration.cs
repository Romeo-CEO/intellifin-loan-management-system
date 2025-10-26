namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Configuration for registering a Camunda worker
/// Defines worker behavior and resource allocation
/// </summary>
public class CamundaWorkerRegistration
{
    /// <summary>
    /// Topic name the worker subscribes to
    /// Format: "client.{process}.{taskName}"
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Job type identifier
    /// Format: "io.intellifin.{domain}.{action}"
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Handler type implementing ICamundaJobHandler
    /// </summary>
    public Type HandlerType { get; set; } = typeof(ICamundaJobHandler);

    /// <summary>
    /// Maximum number of jobs this worker can activate concurrently
    /// Default: 32
    /// </summary>
    public int MaxJobsToActivate { get; set; } = 32;

    /// <summary>
    /// Timeout in seconds for job execution
    /// Job will be retried if not completed within this time
    /// Default: 30 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Worker name for this specific registration
    /// Defaults to HandlerType.Name if not specified
    /// </summary>
    public string? WorkerName { get; set; }
}
