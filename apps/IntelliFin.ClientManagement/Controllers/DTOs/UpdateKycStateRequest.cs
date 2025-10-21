using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Request DTO for updating KYC state
/// </summary>
public class UpdateKycStateRequest
{
    /// <summary>
    /// New KYC state (Pending, InProgress, Completed, EDD_Required, Rejected)
    /// </summary>
    [JsonPropertyName("newState")]
    public string NewState { get; set; } = string.Empty;

    /// <summary>
    /// Notes or reason for state change
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// User ID who completed KYC (for Completed state)
    /// </summary>
    [JsonPropertyName("completedBy")]
    public string? CompletedBy { get; set; }

    // Document completeness flags (for InProgress updates)

    [JsonPropertyName("hasNrc")]
    public bool? HasNrc { get; set; }

    [JsonPropertyName("hasProofOfAddress")]
    public bool? HasProofOfAddress { get; set; }

    [JsonPropertyName("hasPayslip")]
    public bool? HasPayslip { get; set; }

    [JsonPropertyName("hasEmploymentLetter")]
    public bool? HasEmploymentLetter { get; set; }

    // AML screening (for InProgress/Completed updates)

    [JsonPropertyName("amlScreeningComplete")]
    public bool? AmlScreeningComplete { get; set; }

    [JsonPropertyName("amlScreenedBy")]
    public string? AmlScreenedBy { get; set; }

    // EDD fields (for EDD_Required state)

    [JsonPropertyName("requiresEdd")]
    public bool? RequiresEdd { get; set; }

    [JsonPropertyName("eddReason")]
    public string? EddReason { get; set; }

    [JsonPropertyName("eddApprovedBy")]
    public string? EddApprovedBy { get; set; }

    [JsonPropertyName("eddCeoApprovedBy")]
    public string? EddCeoApprovedBy { get; set; }

    [JsonPropertyName("camundaProcessInstanceId")]
    public string? CamundaProcessInstanceId { get; set; }
}
