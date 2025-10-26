using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Services;

namespace IntelliFin.ClientManagement.EventHandlers;

/// <summary>
/// Event handler for KYC rejection notifications
/// Sends SMS notification when KYC verification is rejected
/// </summary>
public class KycRejectedEventHandler : IDomainEventHandler<KycRejectedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly TemplatePersonalizer _templatePersonalizer;
    private readonly ILogger<KycRejectedEventHandler> _logger;

    public KycRejectedEventHandler(
        INotificationService notificationService,
        TemplatePersonalizer templatePersonalizer,
        ILogger<KycRejectedEventHandler> logger)
    {
        _notificationService = notificationService;
        _templatePersonalizer = templatePersonalizer;
        _logger = logger;
    }

    public async Task HandleAsync(KycRejectedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing KYC rejected notification: ClientId={ClientId}, Reason={Reason}, CorrelationId={CorrelationId}",
            domainEvent.ClientId, domainEvent.RejectionReason, domainEvent.CorrelationId);

        try
        {
            // Build personalization data
            var personalizationData = _templatePersonalizer.BuildKycRejectedData(
                clientName: domainEvent.ClientName,
                rejectionDate: domainEvent.RejectedAt,
                rejectionReason: domainEvent.RejectionReason,
                applicationId: null); // Application ID not available at KYC stage

            // Send notification
            var result = await _notificationService.SendKycStatusNotificationAsync(
                clientId: domainEvent.ClientId,
                templateId: "kyc_rejected",
                personalizations: personalizationData,
                correlationId: domainEvent.CorrelationId);

            if (result.IsSuccess && result.Value!.Success)
            {
                _logger.LogInformation(
                    "KYC rejection notification sent successfully: ClientId={ClientId}",
                    domainEvent.ClientId);
            }
            else if (result.IsSuccess && !string.IsNullOrEmpty(result.Value!.BlockedReason))
            {
                _logger.LogInformation(
                    "KYC rejection notification blocked: ClientId={ClientId}, Reason={Reason}",
                    domainEvent.ClientId, result.Value.BlockedReason);
            }
            else
            {
                _logger.LogWarning(
                    "KYC rejection notification failed: ClientId={ClientId}, Error={Error}",
                    domainEvent.ClientId, result.Error ?? result.Value?.FinalError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling KYC rejected event: ClientId={ClientId}",
                domainEvent.ClientId);

            // Don't throw - notification failures should not break the workflow
        }
    }
}
