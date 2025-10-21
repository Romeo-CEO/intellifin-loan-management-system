using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Response DTO for communication consent preferences
/// </summary>
public class ConsentResponse
{
    /// <summary>
    /// Unique consent identifier
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Client ID
    /// </summary>
    [JsonPropertyName("clientId")]
    public Guid ClientId { get; set; }

    /// <summary>
    /// Type of consent (Marketing, Operational, Regulatory)
    /// </summary>
    [JsonPropertyName("consentType")]
    public string ConsentType { get; set; } = string.Empty;

    /// <summary>
    /// SMS channel enabled
    /// </summary>
    [JsonPropertyName("smsEnabled")]
    public bool SmsEnabled { get; set; }

    /// <summary>
    /// Email channel enabled
    /// </summary>
    [JsonPropertyName("emailEnabled")]
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// In-app notification channel enabled
    /// </summary>
    [JsonPropertyName("inAppEnabled")]
    public bool InAppEnabled { get; set; }

    /// <summary>
    /// Phone call channel enabled
    /// </summary>
    [JsonPropertyName("callEnabled")]
    public bool CallEnabled { get; set; }

    /// <summary>
    /// Timestamp when consent was granted
    /// </summary>
    [JsonPropertyName("consentGivenAt")]
    public DateTime ConsentGivenAt { get; set; }

    /// <summary>
    /// Who granted the consent (ClientSelf, Officer, System)
    /// </summary>
    [JsonPropertyName("consentGivenBy")]
    public string ConsentGivenBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when consent was revoked (null if active)
    /// </summary>
    [JsonPropertyName("consentRevokedAt")]
    public DateTime? ConsentRevokedAt { get; set; }

    /// <summary>
    /// Reason for revocation
    /// </summary>
    [JsonPropertyName("revocationReason")]
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Whether consent is currently active
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}
