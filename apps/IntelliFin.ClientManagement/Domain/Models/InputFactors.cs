using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Domain.Models;

/// <summary>
/// Input factors for risk scoring
/// Standardized inputs used by risk scoring rules
/// </summary>
public class InputFactors
{
    // ========== KYC Factors ==========

    /// <summary>
    /// Whether KYC is complete for the client
    /// </summary>
    [JsonPropertyName("kycComplete")]
    public bool KycComplete { get; set; }

    /// <summary>
    /// Current KYC state
    /// </summary>
    [JsonPropertyName("kycState")]
    public string KycState { get; set; } = string.Empty;

    // ========== AML Factors ==========

    /// <summary>
    /// AML risk level (Clear, Low, Medium, High)
    /// </summary>
    [JsonPropertyName("amlRiskLevel")]
    public string AmlRiskLevel { get; set; } = "Clear";

    /// <summary>
    /// Whether client is a Politically Exposed Person
    /// </summary>
    [JsonPropertyName("isPep")]
    public bool IsPep { get; set; }

    /// <summary>
    /// Whether client has sanctions list match
    /// </summary>
    [JsonPropertyName("hasSanctionsHit")]
    public bool HasSanctionsHit { get; set; }

    /// <summary>
    /// Whether AML screening is complete
    /// </summary>
    [JsonPropertyName("amlScreeningComplete")]
    public bool AmlScreeningComplete { get; set; }

    // ========== Document Factors ==========

    /// <summary>
    /// Number of verified documents
    /// </summary>
    [JsonPropertyName("documentCount")]
    public int DocumentCount { get; set; }

    /// <summary>
    /// Whether all required documents are verified
    /// </summary>
    [JsonPropertyName("allDocumentsVerified")]
    public bool AllDocumentsVerified { get; set; }

    /// <summary>
    /// Whether client has NRC document
    /// </summary>
    [JsonPropertyName("hasNrc")]
    public bool HasNrc { get; set; }

    /// <summary>
    /// Whether client has proof of address
    /// </summary>
    [JsonPropertyName("hasProofOfAddress")]
    public bool HasProofOfAddress { get; set; }

    // ========== Client Profile Factors ==========

    /// <summary>
    /// Client age in years
    /// </summary>
    [JsonPropertyName("age")]
    public int Age { get; set; }

    /// <summary>
    /// Whether client is high-value customer
    /// Determined by transaction volume or account balance
    /// </summary>
    [JsonPropertyName("isHighValue")]
    public bool IsHighValue { get; set; }

    /// <summary>
    /// Client's province (for geographic risk)
    /// </summary>
    [JsonPropertyName("province")]
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// Whether client has employer information
    /// </summary>
    [JsonPropertyName("hasEmployer")]
    public bool HasEmployer { get; set; }

    /// <summary>
    /// Source of funds (Salary, Business, Investment, etc.)
    /// </summary>
    [JsonPropertyName("sourceOfFunds")]
    public string SourceOfFunds { get; set; } = string.Empty;

    // ========== EDD Factors ==========

    /// <summary>
    /// Whether client requires EDD
    /// </summary>
    [JsonPropertyName("requiresEdd")]
    public bool RequiresEdd { get; set; }

    /// <summary>
    /// Reason EDD was required (if applicable)
    /// </summary>
    [JsonPropertyName("eddReason")]
    public string? EddReason { get; set; }

    // ========== Metadata ==========

    /// <summary>
    /// When these factors were computed
    /// </summary>
    [JsonPropertyName("computedAt")]
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}
