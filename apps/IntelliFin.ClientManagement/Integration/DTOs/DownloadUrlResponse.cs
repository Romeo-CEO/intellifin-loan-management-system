using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// Response DTO containing pre-signed download URL from KycDocumentService
/// </summary>
public class DownloadUrlResponse
{
    /// <summary>
    /// Pre-signed MinIO URL for secure document download
    /// Valid for limited time (typically 1 hour)
    /// </summary>
    [JsonPropertyName("presignedUrl")]
    public string PresignedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the pre-signed URL expires
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Document ID
    /// </summary>
    [JsonPropertyName("documentId")]
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Original filename
    /// </summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
}
