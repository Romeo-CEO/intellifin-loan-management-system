using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Request DTO for verifying or rejecting a document (dual-control verification)
/// </summary>
public class VerifyDocumentRequest
{
    /// <summary>
    /// Whether to approve the document
    /// true = verify/approve, false = reject
    /// </summary>
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    /// <summary>
    /// Reason for rejection (required if Approved = false)
    /// Examples: "Photo unclear", "Document expired", "Information mismatch"
    /// </summary>
    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; set; }
}
