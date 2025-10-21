using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// Response DTO from KycDocumentService after document upload
/// </summary>
public class UploadDocumentResponse
{
    /// <summary>
    /// Unique document ID assigned by KycDocumentService
    /// </summary>
    [JsonPropertyName("documentId")]
    public Guid DocumentId { get; set; }

    /// <summary>
    /// MinIO object key path (e.g., clients/{clientId}/nrc-{guid}.pdf)
    /// </summary>
    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// MinIO bucket name
    /// </summary>
    [JsonPropertyName("bucketName")]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the file content
    /// </summary>
    [JsonPropertyName("fileHashSha256")]
    public string FileHashSha256 { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Timestamp when file was uploaded
    /// </summary>
    [JsonPropertyName("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Original filename
    /// </summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    /// <summary>
    /// Content type (MIME type)
    /// </summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }
}
