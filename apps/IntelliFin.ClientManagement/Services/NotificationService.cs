using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service implementation for sending notifications via CommunicationsService
/// Includes consent checking to ensure compliance with customer preferences
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ICommunicationsClient _communicationsClient;
    private readonly IConsentManagementService _consentService;
    private readonly IAuditService _auditService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ICommunicationsClient communicationsClient,
        IConsentManagementService consentService,
        IAuditService auditService,
        ILogger<NotificationService> logger)
    {
        _communicationsClient = communicationsClient;
        _consentService = consentService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<SendNotificationResponse?>> SendConsentBasedNotificationAsync(
        Guid clientId,
        string templateId,
        string consentType,
        string channel,
        Dictionary<string, string> personalizationData,
        string userId)
    {
        try
        {
            // Check consent first
            var hasConsent = await _consentService.CheckConsentAsync(clientId, consentType, channel);

            if (!hasConsent)
            {
                _logger.LogWarning(
                    "Notification skipped due to lack of consent: ClientId={ClientId}, Type={ConsentType}, Channel={Channel}, Template={TemplateId}",
                    clientId, consentType, channel, templateId);

                return Result<SendNotificationResponse?>.Success(null);
            }

            // Build notification request
            var request = new SendNotificationRequest
            {
                TemplateId = templateId,
                RecipientId = clientId.ToString(),
                Channel = channel,
                PersonalizationData = personalizationData
            };

            // Send notification
            SendNotificationResponse response;
            try
            {
                response = await _communicationsClient.SendNotificationAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send notification via CommunicationsService: ClientId={ClientId}, Template={TemplateId}",
                    clientId, templateId);
                return Result<SendNotificationResponse?>.Failure(
                    "Failed to send notification. Please try again later.");
            }

            _logger.LogInformation(
                "Notification sent: ClientId={ClientId}, Template={TemplateId}, Channel={Channel}, NotificationId={NotificationId}",
                clientId, templateId, channel, response.NotificationId);

            // Log audit event (fire-and-forget)
            await _auditService.LogAuditEventAsync(
                action: "NotificationSent",
                entityType: "Notification",
                entityId: response.NotificationId.ToString(),
                actor: userId,
                eventData: new
                {
                    ClientId = clientId,
                    TemplateId = templateId,
                    ConsentType = consentType,
                    Channel = channel,
                    Status = response.Status
                });

            return Result<SendNotificationResponse?>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending consent-based notification for client {ClientId}", clientId);
            return Result<SendNotificationResponse?>.Failure($"Error sending notification: {ex.Message}");
        }
    }

    public async Task<Result<SendNotificationResponse>> SendNotificationAsync(
        SendNotificationRequest request,
        string userId)
    {
        try
        {
            _logger.LogInformation(
                "Sending notification without consent check (regulatory): Recipient={RecipientId}, Template={TemplateId}, Channel={Channel}",
                request.RecipientId, request.TemplateId, request.Channel);

            // Send notification directly (no consent check)
            SendNotificationResponse response;
            try
            {
                response = await _communicationsClient.SendNotificationAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send notification via CommunicationsService: Recipient={RecipientId}, Template={TemplateId}",
                    request.RecipientId, request.TemplateId);
                return Result<SendNotificationResponse>.Failure(
                    "Failed to send notification. Please try again later.");
            }

            _logger.LogInformation(
                "Notification sent (regulatory): Recipient={RecipientId}, Template={TemplateId}, NotificationId={NotificationId}",
                request.RecipientId, request.TemplateId, response.NotificationId);

            // Log audit event (fire-and-forget)
            await _auditService.LogAuditEventAsync(
                action: "NotificationSent",
                entityType: "Notification",
                entityId: response.NotificationId.ToString(),
                actor: userId,
                eventData: new
                {
                    RecipientId = request.RecipientId,
                    TemplateId = request.TemplateId,
                    Channel = request.Channel,
                    Status = response.Status,
                    ConsentBypass = true // Indicates regulatory notification
                });

            return Result<SendNotificationResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification for recipient {RecipientId}", request.RecipientId);
            return Result<SendNotificationResponse>.Failure($"Error sending notification: {ex.Message}");
        }
    }
}
