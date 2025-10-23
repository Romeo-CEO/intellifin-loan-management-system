namespace IntelliFin.ClientManagement.Domain.Events;

/// <summary>
/// Domain event published when KYC verification is rejected
/// </summary>
public class KycRejectedEvent
{
    /// <summary>
    /// Client unique identifier
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// KYC status unique identifier
    /// </summary>
    public Guid KycStatusId { get; set; }

    /// <summary>
    /// Client's full name
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// When KYC was rejected
    /// </summary>
    public DateTime RejectedAt { get; set; }

    /// <summary>
    /// Who rejected the KYC
    /// </summary>
    public string RejectedBy { get; set; } = string.Empty;

    /// <summary>
    /// Rejection stage (Compliance, CEO, System)
    /// </summary>
    public string RejectionStage { get; set; } = string.Empty;

    /// <summary>
    /// Reason for rejection
    /// </summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// Whether client can reapply
    /// </summary>
    public bool CanReapply { get; set; } = true;

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Workflow process instance ID
    /// </summary>
    public string? ProcessInstanceId { get; set; }
}
