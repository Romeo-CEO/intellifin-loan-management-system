using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Integration.DTOs;

/// <summary>
/// Response DTO from CommunicationsService after sending a notification
/// </summary>
public class SendNotificationResponse
{
    /// <summary>
    /// Unique notification ID assigned by CommunicationsService
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Current status of the notification
    /// - Queued: Notification queued for delivery
    /// - Sent: Notification successfully sent
    /// - Failed: Notification delivery failed
    /// - Scheduled: Notification scheduled for future delivery
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when notification was sent/queued
    /// </summary>
    [JsonPropertyName("sentAt")]
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Channel used for notification
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Error message if status is Failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
