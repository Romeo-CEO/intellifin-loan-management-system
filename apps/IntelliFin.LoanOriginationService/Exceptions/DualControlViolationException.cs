namespace IntelliFin.LoanOriginationService.Exceptions;

/// <summary>
/// Exception thrown when a loan approval violates dual control (segregation of duties) requirements.
/// Prevents users from approving loans they created or assessed.
/// </summary>
public class DualControlViolationException : Exception
{
    /// <summary>
    /// The ID of the loan application that was subject to the violation attempt.
    /// </summary>
    public Guid ApplicationId { get; }
    
    /// <summary>
    /// The user ID of the approver who attempted to violate dual control.
    /// </summary>
    public string ApproverUserId { get; }
    
    /// <summary>
    /// The type of dual control violation (e.g., SELF_APPROVAL, APPROVER_AS_ASSESSOR).
    /// </summary>
    public string ViolationType { get; }
    
    /// <summary>
    /// Initializes a new instance of the DualControlViolationException.
    /// </summary>
    /// <param name="applicationId">The loan application ID.</param>
    /// <param name="approverUserId">The approver user ID attempting the violation.</param>
    /// <param name="message">Detailed error message explaining the violation.</param>
    public DualControlViolationException(
        Guid applicationId, 
        string approverUserId, 
        string message) 
        : base(message)
    {
        ApplicationId = applicationId;
        ApproverUserId = approverUserId;
        ViolationType = DetermineViolationType(message);
    }
    
    /// <summary>
    /// Determines the violation type based on the error message content.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A violation type code (SELF_APPROVAL, APPROVER_AS_ASSESSOR, UNKNOWN_VIOLATION).</returns>
    private string DetermineViolationType(string message)
    {
        if (message.Contains("own loan", StringComparison.OrdinalIgnoreCase)) 
            return "SELF_APPROVAL";
        if (message.Contains("assessed", StringComparison.OrdinalIgnoreCase)) 
            return "APPROVER_AS_ASSESSOR";
        return "UNKNOWN_VIOLATION";
    }
}
