using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// Request DTO for uploading a document to KycDocumentService
/// Sent as part of multipart/form-data request
/// </summary>
public class UploadDocumentRequest
{
    /// <summary>
    /// Type of document (NRC, Payslip, etc.)
    /// </summary>
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Client ID the document belongs to
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Document category (KYC, Loan, Compliance, General)
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}
