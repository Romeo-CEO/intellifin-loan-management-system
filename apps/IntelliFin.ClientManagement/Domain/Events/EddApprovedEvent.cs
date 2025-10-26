namespace IntelliFin.ClientManagement.Domain.Events;

/// <summary>
/// Domain event published when EDD is approved by CEO
/// </summary>
public class EddApprovedEvent
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
    /// Compliance officer who performed first-level review
    /// </summary>
    public string ComplianceApprovedBy { get; set; } = string.Empty;

    /// <summary>
    /// Compliance officer comments/notes
    /// </summary>
    public string? ComplianceComments { get; set; }

    /// <summary>
    /// CEO who provided final approval
    /// </summary>
    public string CeoApprovedBy { get; set; } = string.Empty;

    /// <summary>
    /// CEO decision rationale
    /// </summary>
    public string? CeoComments { get; set; }

    /// <summary>
    /// Risk acceptance level set by CEO
    /// Values: Standard, EnhancedMonitoring, RestrictedServices
    /// </summary>
    public string RiskAcceptanceLevel { get; set; } = string.Empty;

    /// <summary>
    /// When EDD was approved
    /// </summary>
    public DateTime ApprovedAt { get; set; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Camunda process instance ID for the EDD workflow
    /// </summary>
    public string? ProcessInstanceId { get; set; }
}
