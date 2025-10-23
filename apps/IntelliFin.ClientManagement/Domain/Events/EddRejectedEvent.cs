namespace IntelliFin.ClientManagement.Domain.Events;

/// <summary>
/// Domain event published when EDD is rejected (by Compliance or CEO)
/// </summary>
public class EddRejectedEvent
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
    /// Client full name
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// User who rejected the EDD
    /// </summary>
    public string RejectedBy { get; set; } = string.Empty;

    /// <summary>
    /// Stage at which rejection occurred
    /// Values: Compliance, CEO
    /// </summary>
    public string RejectionStage { get; set; } = string.Empty;

    /// <summary>
    /// Reason for rejection
    /// </summary>
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>
    /// When EDD was rejected
    /// </summary>
    public DateTime RejectedAt { get; set; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Camunda process instance ID for the EDD workflow
    /// </summary>
    public string? ProcessInstanceId { get; set; }
}
