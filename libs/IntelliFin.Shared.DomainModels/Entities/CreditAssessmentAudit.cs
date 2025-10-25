using System.ComponentModel.DataAnnotations;

namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Comprehensive audit trail for credit assessment decisions.
/// Tracks all events during the assessment lifecycle for regulatory compliance.
/// Created in Story 1.2 for Credit Assessment microservice.
/// </summary>
public class CreditAssessmentAudit
{
    /// <summary>
    /// Unique identifier for the audit record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the credit assessment being audited.
    /// </summary>
    [Required]
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Type of audit event (e.g., Initiated, RuleEvaluated, DecisionMade, Invalidated, ManualOverride).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload containing event-specific data (inputs, outputs, calculations).
    /// </summary>
    [Required]
    public string EventPayload { get; set; } = string.Empty;

    /// <summary>
    /// User ID who triggered the event, if applicable.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for tracing requests across microservices.
    /// </summary>
    [MaxLength(200)]
    public string? CorrelationId { get; set; }

    // Navigation Properties
    
    /// <summary>
    /// Navigation property to the associated credit assessment.
    /// </summary>
    public CreditAssessment? Assessment { get; set; }
}
