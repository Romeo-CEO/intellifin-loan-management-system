namespace IntelliFin.ClientManagement.Domain.Events;

/// <summary>
/// Domain event published when KYC verification is completed successfully
/// </summary>
public class KycCompletedEvent
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
    /// When KYC was completed
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Who completed the KYC (user ID or system)
    /// </summary>
    public string CompletedBy { get; set; } = string.Empty;

    /// <summary>
    /// Risk rating assigned
    /// </summary>
    public string RiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Risk score calculated
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Whether EDD was required (but approved)
    /// </summary>
    public bool EddRequired { get; set; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Workflow process instance ID
    /// </summary>
    public string? ProcessInstanceId { get; set; }
}
