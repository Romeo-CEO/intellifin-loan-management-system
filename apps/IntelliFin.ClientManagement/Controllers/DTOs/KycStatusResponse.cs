using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Response DTO for KYC status information
/// </summary>
public class KycStatusResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("clientId")]
    public Guid ClientId { get; set; }

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("currentState")]
    public string CurrentState { get; set; } = string.Empty;

    [JsonPropertyName("kycStartedAt")]
    public DateTime? KycStartedAt { get; set; }

    [JsonPropertyName("kycCompletedAt")]
    public DateTime? KycCompletedAt { get; set; }

    [JsonPropertyName("kycCompletedBy")]
    public string? KycCompletedBy { get; set; }

    [JsonPropertyName("camundaProcessInstanceId")]
    public string? CamundaProcessInstanceId { get; set; }

    [JsonPropertyName("hasNrc")]
    public bool HasNrc { get; set; }

    [JsonPropertyName("hasProofOfAddress")]
    public bool HasProofOfAddress { get; set; }

    [JsonPropertyName("hasPayslip")]
    public bool HasPayslip { get; set; }

    [JsonPropertyName("hasEmploymentLetter")]
    public bool HasEmploymentLetter { get; set; }

    [JsonPropertyName("isDocumentComplete")]
    public bool IsDocumentComplete { get; set; }

    [JsonPropertyName("amlScreeningComplete")]
    public bool AmlScreeningComplete { get; set; }

    [JsonPropertyName("amlScreenedAt")]
    public DateTime? AmlScreenedAt { get; set; }

    [JsonPropertyName("requiresEdd")]
    public bool RequiresEdd { get; set; }

    [JsonPropertyName("eddReason")]
    public string? EddReason { get; set; }

    [JsonPropertyName("eddEscalatedAt")]
    public DateTime? EddEscalatedAt { get; set; }

    [JsonPropertyName("eddApprovedBy")]
    public string? EddApprovedBy { get; set; }

    [JsonPropertyName("eddCeoApprovedBy")]
    public string? EddCeoApprovedBy { get; set; }

    [JsonPropertyName("eddApprovedAt")]
    public DateTime? EddApprovedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
