namespace IntelliFin.LoanOriginationService.Events;

/// <summary>
/// Event published when a loan approval is attempted, whether successful or blocked by dual control validation.
/// Used for audit trail and compliance reporting.
/// </summary>
public class LoanApprovalAttempted
{
    /// <summary>
    /// The ID of the loan application being approved.
    /// </summary>
    public Guid LoanApplicationId { get; set; }
    
    /// <summary>
    /// The loan number for easy reference in logs and audit reports.
    /// </summary>
    public string LoanNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// The user ID of the person attempting to approve the loan.
    /// </summary>
    public string ApproverUserId { get; set; } = string.Empty;
    
    /// <summary>
    /// The user ID of the person who created the loan application (for dual control check).
    /// </summary>
    public string? CreatedByUserId { get; set; }
    
    /// <summary>
    /// The user ID of the person who assessed the loan (for dual control check), if applicable.
    /// </summary>
    public string? AssessedByUserId { get; set; }
    
    /// <summary>
    /// The IP address from which the approval attempt was made (for audit trail).
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// The outcome of the approval attempt.
    /// Possible values: "VALIDATION_PASSED", "BLOCKED_SELF_APPROVAL", "BLOCKED_APPROVER_AS_ASSESSOR".
    /// </summary>
    public string Outcome { get; set; } = string.Empty;
    
    /// <summary>
    /// The timestamp when the approval was attempted (UTC).
    /// </summary>
    public DateTime AttemptedAt { get; set; }
    
    /// <summary>
    /// Correlation ID for distributed tracing and log correlation.
    /// </summary>
    public Guid CorrelationId { get; set; }
}
