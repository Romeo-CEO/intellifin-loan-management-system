namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Configuration options for audit event batching behavior.
/// </summary>
public sealed class AuditBatchingOptions
{
    public int BatchSize { get; set; } = 100;
    public int BatchIntervalSeconds { get; set; } = 5;
    public bool EnableDeadLetterQueue { get; set; } = true;
    public string DeadLetterQueuePath { get; set; } = "logs/audit-dlq.jsonl";
}
