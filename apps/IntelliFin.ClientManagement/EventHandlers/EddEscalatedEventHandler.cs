using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Services;

namespace IntelliFin.ClientManagement.EventHandlers;

/// <summary>
/// Event handler for EDD escalation notifications
/// Sends SMS notification when client is escalated to Enhanced Due Diligence
/// </summary>
public class EddEscalatedEventHandler : IDomainEventHandler<EddEscalatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly TemplatePersonalizer _templatePersonalizer;
    private readonly ILogger<EddEscalatedEventHandler> _logger;

    public EddEscalatedEventHandler(
        INotificationService notificationService,
        TemplatePersonalizer templatePersonalizer,
        ILogger<EddEscalatedEventHandler> logger)
    {
        _notificationService = notificationService;
        _templatePersonalizer = templatePersonalizer;
        _logger = logger;
    }

    public async Task HandleAsync(EddEscalatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing EDD escalation notification: ClientId={ClientId}, Reason={Reason}, CorrelationId={CorrelationId}",
            domainEvent.ClientId, domainEvent.EddReason, domainEvent.CorrelationId);

        try
        {
            // Build personalization data
            var personalizationData = _templatePersonalizer.BuildEddEscalationData(
                clientName: domainEvent.ClientName,
                escalationDate: domainEvent.EscalatedAt,
                eddReason: domainEvent.EddReason,
                expectedTimeframe: domainEvent.ExpectedTimeframe);

            // Send notification
            var result = await _notificationService.SendKycStatusNotificationAsync(
                clientId: domainEvent.ClientId,
                templateId: "kyc_edd_required",
                personalizations: personalizationData,
                correlationId: domainEvent.CorrelationId);

            if (result.IsSuccess && result.Value!.Success)
            {
                _logger.LogInformation(
                    "EDD escalation notification sent successfully: ClientId={ClientId}",
                    domainEvent.ClientId);
            }
            else if (result.IsSuccess && !string.IsNullOrEmpty(result.Value!.BlockedReason))
            {
                _logger.LogInformation(
                    "EDD escalation notification blocked: ClientId={ClientId}, Reason={Reason}",
                    domainEvent.ClientId, result.Value.BlockedReason);
            }
            else
            {
                _logger.LogWarning(
                    "EDD escalation notification failed: ClientId={ClientId}, Error={Error}",
                    domainEvent.ClientId, result.Error ?? result.Value?.FinalError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling EDD escalation event: ClientId={ClientId}",
                domainEvent.ClientId);

            // Don't throw - notification failures should not break the workflow
        }
    }
}
