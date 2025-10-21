using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Integration.DTOs;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service interface for sending notifications via CommunicationsService
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a consent-based notification
    /// Checks consent before sending; does not send if consent not granted
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="templateId">Notification template ID</param>
    /// <param name="consentType">Type of consent required (Marketing, Operational)</param>
    /// <param name="channel">Communication channel (SMS, Email, InApp, Call)</param>
    /// <param name="personalizationData">Template personalization data</param>
    /// <param name="userId">User requesting the notification (for audit)</param>
    /// <returns>Notification response or null if consent not granted</returns>
    Task<Result<SendNotificationResponse?>> SendConsentBasedNotificationAsync(
        Guid clientId,
        string templateId,
        string consentType,
        string channel,
        Dictionary<string, string> personalizationData,
        string userId);

    /// <summary>
    /// Sends a notification without consent check
    /// Use only for regulatory/legally required notifications
    /// </summary>
    /// <param name="request">Notification request</param>
    /// <param name="userId">User requesting the notification (for audit)</param>
    /// <returns>Notification response</returns>
    Task<Result<SendNotificationResponse>> SendNotificationAsync(
        SendNotificationRequest request,
        string userId);
}
