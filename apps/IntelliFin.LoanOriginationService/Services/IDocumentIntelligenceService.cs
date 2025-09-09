using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Document Intelligence Service for OCR and automated data extraction
/// Part of the System-Assisted Manual Verification model
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Processes uploaded document image using OCR to extract structured data
    /// </summary>
    Task<OcrDocumentData> ExtractDocumentDataAsync(string imagePath, string documentType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compares manually entered data with OCR extracted data to identify mismatches
    /// </summary>
    Task<List<DataMismatch>> CompareDataAsync(ManualEntryData manualData, OcrDocumentData ocrData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new document verification record for loan officer review
    /// </summary>
    Task<DocumentVerification> CreateVerificationRecordAsync(
        Guid clientId, 
        string documentType, 
        string imagePath,
        ManualEntryData manualData,
        OcrDocumentData ocrData,
        List<DataMismatch> mismatches,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Completes the verification process with loan officer decision
    /// </summary>
    Task<DocumentVerification> CompleteVerificationAsync(
        Guid verificationId, 
        bool isVerified, 
        string verifiedBy, 
        string notes, 
        string decisionReason,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending verifications for loan officer review
    /// </summary>
    Task<IEnumerable<DocumentVerification>> GetPendingVerificationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Document verification workflow models
/// </summary>
public class DocumentVerificationRequest
{
    public Guid ClientId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentImagePath { get; set; } = string.Empty;
    public ManualEntryData ManualData { get; set; } = new();
}

public class VerificationDecisionRequest
{
    public Guid VerificationId { get; set; }
    public bool IsVerified { get; set; }
    public string VerifiedBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
}