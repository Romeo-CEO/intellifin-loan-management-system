namespace IntelliFin.LoanOriginationService.Models;

/// <summary>
/// Represents the result of agreement generation, containing file metadata and storage information.
/// </summary>
public class AgreementDocument
{
    /// <summary>
    /// The loan number associated with this agreement.
    /// </summary>
    public string LoanNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// SHA256 hash of the generated PDF for integrity verification and tamper detection.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;
    
    /// <summary>
    /// MinIO storage path where the PDF is stored (e.g., loan-agreements/{clientId}/{loanNumber}_v{version}.pdf).
    /// </summary>
    public string MinioPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when the agreement was generated (UTC).
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}
