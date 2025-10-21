using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// Request DTO for sending notifications via CommunicationsService
/// </summary>
public class SendNotificationRequest
{
    /// <summary>
    /// Template ID for the notification (e.g., "kyc_approved", "document_expiring_soon")
    /// Must match a template defined in CommunicationsService
    /// </summary>
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    /// Recipient client ID
    /// </summary>
    [JsonPropertyName("recipientId")]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>
    /// Communication channel (SMS, Email, InApp, Push)
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Template personalization data (key-value pairs)
    /// Example: { "clientName": "John Doe", "kycStatus": "Approved" }
    /// </summary>
    [JsonPropertyName("personalizationData")]
    public Dictionary<string, string> PersonalizationData { get; set; } = new();

    /// <summary>
    /// Optional scheduled send time (ISO 8601 format)
    /// If null, notification sent immediately
    /// </summary>
    [JsonPropertyName("scheduledFor")]
    public string? ScheduledFor { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}
