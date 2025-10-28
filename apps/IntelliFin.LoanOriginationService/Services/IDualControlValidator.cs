namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service for validating dual control (segregation of duties) requirements for loan approvals.
/// Ensures that approvers cannot approve loans they created or assessed.
/// </summary>
public interface IDualControlValidator
{
    /// <summary>
    /// Validates that the approver is allowed to approve the loan application under dual control rules.
    /// Throws DualControlViolationException if validation fails.
    /// </summary>
    /// <param name="applicationId">The ID of the loan application being approved.</param>
    /// <param name="approverUserId">The user ID of the person attempting to approve the loan.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="DualControlViolationException">Thrown when the approver violates dual control rules.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the loan application is not found.</exception>
    /// <remarks>
    /// Dual control rules:
    /// 1. Approver cannot be the same person who created the loan application (no self-approval).
    /// 2. Approver cannot be the same person who performed the credit assessment.
    /// 
    /// This method also publishes a LoanApprovalAttempted audit event for all attempts,
    /// whether successful or blocked, to maintain a complete audit trail.
    /// </remarks>
    Task ValidateApprovalAsync(
        Guid applicationId, 
        string approverUserId,
        CancellationToken cancellationToken);
}
