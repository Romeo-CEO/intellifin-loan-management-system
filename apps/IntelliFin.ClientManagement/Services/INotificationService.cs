using IntelliFin.ClientManagement.Common;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for sending notifications to clients
/// Handles consent checking, template personalization, and retry logic
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a KYC status notification to a client
    /// Checks consent before sending
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="templateId">Notification template ID</param>
    /// <param name="personalizations">Template personalization data</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    Task<Result<NotificationResult>> SendKycStatusNotificationAsync(
        Guid clientId,
        string templateId,
        Dictionary<string, object> personalizations,
        string? correlationId = null);

    /// <summary>
    /// Checks if client has consented to receive notifications
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="channel">Notification channel (SMS, Email, etc.)</param>
    Task<bool> CheckNotificationConsentAsync(Guid clientId, NotificationChannel channel);

    /// <summary>
    /// Sends notification with retry logic
    /// </summary>
    /// <param name="request">Notification request</param>
    Task<Result<NotificationResult>> SendNotificationWithRetryAsync(NotificationRequest request);
}

/// <summary>
/// Notification channel enum
/// </summary>
public enum NotificationChannel
{
    SMS,
    Email,
    InApp
}
