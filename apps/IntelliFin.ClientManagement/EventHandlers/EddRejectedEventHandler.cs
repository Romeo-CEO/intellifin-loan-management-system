using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Services;

namespace IntelliFin.ClientManagement.EventHandlers;

/// <summary>
/// Event handler for EDD rejection notifications
/// Sends SMS notification when EDD review is rejected
/// </summary>
public class EddRejectedEventHandler : IDomainEventHandler<EddRejectedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly TemplatePersonalizer _templatePersonalizer;
    private readonly ILogger<EddRejectedEventHandler> _logger;

    public EddRejectedEventHandler(
        INotificationService notificationService,
        TemplatePersonalizer templatePersonalizer,
        ILogger<EddRejectedEventHandler> logger)
    {
        _notificationService = notificationService;
        _templatePersonalizer = templatePersonalizer;
        _logger = logger;
    }

    public async Task HandleAsync(EddRejectedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing EDD rejection notification: ClientId={ClientId}, Stage={Stage}, CorrelationId={CorrelationId}",
            domainEvent.ClientId, domainEvent.RejectionStage, domainEvent.CorrelationId);

        try
        {
            // Build personalization data
            var personalizationData = _templatePersonalizer.BuildEddRejectedData(
                clientName: domainEvent.ClientName,
                rejectionDate: domainEvent.RejectedAt,
                rejectionReason: domainEvent.RejectionReason,
                rejectionStage: domainEvent.RejectionStage);

            // Send notification
            var result = await _notificationService.SendKycStatusNotificationAsync(
                clientId: domainEvent.ClientId,
                templateId: "edd_rejected",
                personalizations: personalizationData,
                correlationId: domainEvent.CorrelationId);

            if (result.IsSuccess && result.Value!.Success)
            {
                _logger.LogInformation(
                    "EDD rejection notification sent successfully: ClientId={ClientId}",
                    domainEvent.ClientId);
            }
            else if (result.IsSuccess && !string.IsNullOrEmpty(result.Value!.BlockedReason))
            {
                _logger.LogInformation(
                    "EDD rejection notification blocked: ClientId={ClientId}, Reason={Reason}",
                    domainEvent.ClientId, result.Value.BlockedReason);
            }
            else
            {
                _logger.LogWarning(
                    "EDD rejection notification failed: ClientId={ClientId}, Error={Error}",
                    domainEvent.ClientId, result.Error ?? result.Value?.FinalError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling EDD rejected event: ClientId={ClientId}",
                domainEvent.ClientId);

            // Don't throw - notification failures should not break the workflow
        }
    }
}
