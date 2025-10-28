namespace IntelliFin.LoanOriginationService.Events;

/// <summary>
/// Event published when a loan agreement is successfully generated and stored.
/// Used for audit trail, compliance reporting, and downstream processing.
/// </summary>
public class LoanAgreementGenerated
{
    /// <summary>
    /// The ID of the loan application for which the agreement was generated.
    /// </summary>
    public Guid LoanApplicationId { get; set; }
    
    /// <summary>
    /// The loan number for easy reference in logs and audit reports.
    /// </summary>
    public string LoanNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// The client ID associated with this loan agreement.
    /// </summary>
    public Guid ClientId { get; set; }
    
    /// <summary>
    /// SHA256 hash of the generated PDF for integrity verification and tamper detection.
    /// </summary>
    public string DocumentHash { get; set; } = string.Empty;
    
    /// <summary>
    /// MinIO storage path where the PDF is stored.
    /// </summary>
    public string MinioPath { get; set; } = string.Empty;
    
    /// <summary>
    /// The version of the agreement template used for generation (e.g., "GEPL-v1.0").
    /// </summary>
    public string TemplateVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when the agreement was generated (UTC).
    /// </summary>
    public DateTime GeneratedAt { get; set; }
    
    /// <summary>
    /// User ID who triggered the generation, or "SYSTEM" for automated generation.
    /// </summary>
    public string GeneratedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Correlation ID for distributed tracing and log correlation.
    /// </summary>
    public Guid CorrelationId { get; set; }
}
