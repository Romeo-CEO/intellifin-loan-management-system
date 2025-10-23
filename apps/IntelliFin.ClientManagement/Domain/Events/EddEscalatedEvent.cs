namespace IntelliFin.ClientManagement.Domain.Events;

/// <summary>
/// Domain event published when a client is escalated to Enhanced Due Diligence (EDD)
/// </summary>
public class EddEscalatedEvent
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
    /// When EDD was triggered
    /// </summary>
    public DateTime EscalatedAt { get; set; }

    /// <summary>
    /// Reason EDD was triggered
    /// (e.g., "Sanctions", "PEP", "High Risk", "Multiple Medium Risk")
    /// </summary>
    public string EddReason { get; set; } = string.Empty;

    /// <summary>
    /// Overall risk level that triggered EDD
    /// </summary>
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Whether there was a sanctions hit
    /// </summary>
    public bool HasSanctionsHit { get; set; }

    /// <summary>
    /// Whether client is a PEP
    /// </summary>
    public bool IsPep { get; set; }

    /// <summary>
    /// Expected timeframe for EDD review
    /// </summary>
    public string ExpectedTimeframe { get; set; } = "5-7 business days";

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Workflow process instance ID
    /// </summary>
    public string? ProcessInstanceId { get; set; }
}
