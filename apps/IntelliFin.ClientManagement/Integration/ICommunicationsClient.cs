using IntelliFin.ClientManagement.Integration.DTOs;
using Refit;

namespace IntelliFin.ClientManagement.Integration;

/// <summary>
/// Refit interface for CommunicationsService API
/// Handles multi-channel notification delivery (SMS, Email, InApp, Push)
/// </summary>
public interface ICommunicationsClient
{
    /// <summary>
    /// Sends a notification via CommunicationsService
    /// </summary>
    /// <param name="request">Notification request with template, recipient, and personalization data</param>
    /// <returns>Notification response with ID and status</returns>
    [Post("/api/communications/send")]
    Task<SendNotificationResponse> SendNotificationAsync([Body] SendNotificationRequest request);
}
