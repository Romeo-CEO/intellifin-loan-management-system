using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// Response DTO containing document metadata from KycDocumentService
/// </summary>
public class DocumentMetadataResponse
{
    /// <summary>
    /// Document unique identifier
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Client ID the document belongs to
    /// </summary>
    [JsonPropertyName("clientId")]
    public Guid ClientId { get; set; }

    /// <summary>
    /// Document type (NRC, Payslip, etc.)
    /// </summary>
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Document category (KYC, Loan, Compliance, General)
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// MinIO object key
    /// </summary>
    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// MinIO bucket name
    /// </summary>
    [JsonPropertyName("bucketName")]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Original filename
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Content type (MIME type)
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// SHA256 hash for integrity verification
    /// </summary>
    [JsonPropertyName("fileHashSha256")]
    public string FileHashSha256 { get; set; } = string.Empty;

    /// <summary>
    /// Upload status
    /// </summary>
    [JsonPropertyName("uploadStatus")]
    public string UploadStatus { get; set; } = string.Empty;

    /// <summary>
    /// Upload timestamp
    /// </summary>
    [JsonPropertyName("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// User who uploaded the document
    /// </summary>
    [JsonPropertyName("uploadedBy")]
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>
    /// Verification timestamp (null if not verified)
    /// </summary>
    [JsonPropertyName("verifiedAt")]
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// User who verified the document
    /// </summary>
    [JsonPropertyName("verifiedBy")]
    public string? VerifiedBy { get; set; }

    /// <summary>
    /// Document expiry date
    /// </summary>
    [JsonPropertyName("expiryDate")]
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// BoZ retention until date (7 years)
    /// </summary>
    [JsonPropertyName("retentionUntil")]
    public DateTime RetentionUntil { get; set; }
}
