namespace IntelliFin.LoanOriginationService.Events;

/// <summary>
/// Event published by Client Management Service when a client's KYC status is revoked.
/// This triggers workflow pause for all active loan applications for the affected client.
/// </summary>
public class ClientKycRevoked
{
    /// <summary>
    /// Unique identifier of the client whose KYC was revoked.
    /// </summary>
    public Guid ClientId { get; set; }
    
    /// <summary>
    /// Timestamp when the KYC was revoked (UTC).
    /// </summary>
    public DateTime RevokedAt { get; set; }
    
    /// <summary>
    /// Reason for KYC revocation (e.g., "COMPLIANCE_ISSUE", "DOCUMENT_EXPIRED", "FRAUD_DETECTED").
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// User ID of the person who revoked the KYC.
    /// </summary>
    public string RevokedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Correlation ID for distributed tracing across services.
    /// </summary>
    public Guid CorrelationId { get; set; }
}
