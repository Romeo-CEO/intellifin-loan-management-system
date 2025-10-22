using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Services;

namespace IntelliFin.ClientManagement.EventHandlers;

/// <summary>
/// Event handler for KYC completion notifications
/// Sends SMS notification when KYC verification is completed
/// </summary>
public class KycCompletedEventHandler : IDomainEventHandler<KycCompletedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly TemplatePersonalizer _templatePersonalizer;
    private readonly ILogger<KycCompletedEventHandler> _logger;

    public KycCompletedEventHandler(
        INotificationService notificationService,
        TemplatePersonalizer templatePersonalizer,
        ILogger<KycCompletedEventHandler> logger)
    {
        _notificationService = notificationService;
        _templatePersonalizer = templatePersonalizer;
        _logger = logger;
    }

    public async Task HandleAsync(KycCompletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing KYC completed notification: ClientId={ClientId}, CorrelationId={CorrelationId}",
            domainEvent.ClientId, domainEvent.CorrelationId);

        try
        {
            // Build personalization data
            var personalizationData = _templatePersonalizer.BuildKycApprovedData(
                clientName: domainEvent.ClientName,
                completionDate: domainEvent.CompletedAt,
                nextSteps: "Your loan application will proceed to the next stage");

            // Send notification
            var result = await _notificationService.SendKycStatusNotificationAsync(
                clientId: domainEvent.ClientId,
                templateId: "kyc_approved",
                personalizations: personalizationData,
                correlationId: domainEvent.CorrelationId);

            if (result.IsSuccess && result.Value!.Success)
            {
                _logger.LogInformation(
                    "KYC completion notification sent successfully: ClientId={ClientId}",
                    domainEvent.ClientId);
            }
            else if (result.IsSuccess && !string.IsNullOrEmpty(result.Value!.BlockedReason))
            {
                _logger.LogInformation(
                    "KYC completion notification blocked: ClientId={ClientId}, Reason={Reason}",
                    domainEvent.ClientId, result.Value.BlockedReason);
            }
            else
            {
                _logger.LogWarning(
                    "KYC completion notification failed: ClientId={ClientId}, Error={Error}",
                    domainEvent.ClientId, result.Error ?? result.Value?.FinalError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling KYC completed event: ClientId={ClientId}",
                domainEvent.ClientId);

            // Don't throw - notification failures should not break the workflow
            // In production, this would be sent to a DLQ for retry
        }
    }
}
