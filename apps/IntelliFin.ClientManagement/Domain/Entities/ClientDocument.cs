using IntelliFin.ClientManagement.Domain.Enums;

namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// Represents a document uploaded for a client (KYC, Loan, Compliance documents)
/// Stored in MinIO via KycDocumentService with 7-year retention for BoZ compliance
/// </summary>
public class ClientDocument
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to Client entity
    /// </summary>
    public Guid ClientId { get; set; }

    // ========== Document Classification ==========

    /// <summary>
    /// Type of document (NRC, Payslip, ProofOfResidence, EmploymentLetter, BankStatement)
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Category of document (KYC, Loan, Compliance, General)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    // ========== MinIO Storage ==========

    /// <summary>
    /// MinIO object path (e.g., clients/{clientId}/nrc-{guid}.pdf)
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// MinIO bucket name (e.g., kyc-documents)
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Original filename uploaded by user
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (application/pdf, image/jpeg, image/png)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// SHA256 hash of file content for integrity verification
    /// </summary>
    public string FileHashSha256 { get; set; } = string.Empty;

    // ========== Dual-Control Workflow ==========

    /// <summary>
    /// Upload and verification status
    /// Tracks document lifecycle from upload through dual-control verification
    /// </summary>
    public UploadStatus UploadStatus { get; set; } = UploadStatus.Uploaded;

    /// <summary>
    /// Timestamp when document was uploaded
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// User ID of officer who uploaded the document
    /// </summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when document was verified (null if not yet verified)
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// User ID of officer who verified the document (must be different from UploadedBy)
    /// </summary>
    public string? VerifiedBy { get; set; }

    /// <summary>
    /// Reason for rejection if document was rejected
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Camunda process instance ID for workflow tracking (Story 1.11)
    /// </summary>
    public string? CamundaProcessInstanceId { get; set; }

    // ========== Compliance ==========

    /// <summary>
    /// Expiry date for documents that expire (e.g., NRC, employment letter)
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Bank of Zambia 7-year retention policy - document cannot be deleted before this date
    /// Calculated as UploadedAt + 7 years
    /// </summary>
    public DateTime RetentionUntil { get; set; }

    /// <summary>
    /// Flag indicating if document has been archived (soft delete)
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Timestamp when document was archived
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    // ========== OCR Metadata (Future) ==========

    /// <summary>
    /// JSON string containing extracted data from OCR processing
    /// </summary>
    public string? ExtractedDataJson { get; set; }

    /// <summary>
    /// OCR confidence score (0.0 to 1.0)
    /// </summary>
    public float? OcrConfidenceScore { get; set; }

    // ========== Audit ==========

    /// <summary>
    /// Timestamp when record was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    public string? CorrelationId { get; set; }

    // ========== Navigation Properties ==========

    /// <summary>
    /// Navigation property to parent Client
    /// </summary>
    public Client Client { get; set; } = null!;
}


/// <summary>
/// Document type enumeration values
/// </summary>
public static class DocumentType
{
    public const string NRC = "NRC";
    public const string Payslip = "Payslip";
    public const string ProofOfResidence = "ProofOfResidence";
    public const string EmploymentLetter = "EmploymentLetter";
    public const string BankStatement = "BankStatement";
    public const string Other = "Other";
}

/// <summary>
/// Document category enumeration values
/// </summary>
public static class DocumentCategory
{
    public const string KYC = "KYC";
    public const string Loan = "Loan";
    public const string Compliance = "Compliance";
    public const string General = "General";
}
