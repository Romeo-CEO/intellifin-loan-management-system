namespace IntelliFin.LoanOriginationService.Events;

/// <summary>
/// Audit event published when a loan application workflow is paused due to compliance issues.
/// Published to Admin Service for audit trail and reporting.
/// </summary>
public class LoanApplicationPaused
{
    /// <summary>
    /// Unique identifier of the loan application that was paused.
    /// </summary>
    public Guid LoanApplicationId { get; set; }
    
    /// <summary>
    /// Unique identifier of the client associated with the loan application.
    /// </summary>
    public Guid ClientId { get; set; }
    
    /// <summary>
    /// Loan number for human-readable identification.
    /// </summary>
    public string LoanNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Zeebe workflow instance ID that was paused.
    /// </summary>
    public string? WorkflowInstanceId { get; set; }
    
    /// <summary>
    /// Reason for pausing the workflow (e.g., "KYC_REVOKED: COMPLIANCE_ISSUE").
    /// </summary>
    public string PausedReason { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when the workflow was paused (UTC).
    /// </summary>
    public DateTime PausedAt { get; set; }
    
    /// <summary>
    /// Timestamp when the KYC was revoked (if applicable).
    /// </summary>
    public DateTime? KycRevokedAt { get; set; }
    
    /// <summary>
    /// Correlation ID for distributed tracing across services.
    /// </summary>
    public Guid CorrelationId { get; set; }
}
