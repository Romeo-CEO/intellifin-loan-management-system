using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Request DTO for updating communication consent preferences
/// </summary>
public class UpdateConsentRequest
{
    /// <summary>
    /// Type of consent (Marketing, Operational, Regulatory)
    /// </summary>
    [JsonPropertyName("consentType")]
    public string ConsentType { get; set; } = string.Empty;

    /// <summary>
    /// Enable/disable SMS channel
    /// </summary>
    [JsonPropertyName("smsEnabled")]
    public bool SmsEnabled { get; set; }

    /// <summary>
    /// Enable/disable Email channel
    /// </summary>
    [JsonPropertyName("emailEnabled")]
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// Enable/disable In-App notification channel
    /// </summary>
    [JsonPropertyName("inAppEnabled")]
    public bool InAppEnabled { get; set; }

    /// <summary>
    /// Enable/disable Phone call channel
    /// </summary>
    [JsonPropertyName("callEnabled")]
    public bool CallEnabled { get; set; }

    /// <summary>
    /// Reason for revoking consent (required if all channels disabled)
    /// </summary>
    [JsonPropertyName("revocationReason")]
    public string? RevocationReason { get; set; }
}
