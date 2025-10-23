using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Services;

namespace IntelliFin.ClientManagement.EventHandlers;

/// <summary>
/// Event handler for EDD approval notifications
/// Sends SMS notification when EDD review is approved
/// </summary>
public class EddApprovedEventHandler : IDomainEventHandler<EddApprovedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly TemplatePersonalizer _templatePersonalizer;
    private readonly ILogger<EddApprovedEventHandler> _logger;

    public EddApprovedEventHandler(
        INotificationService notificationService,
        TemplatePersonalizer templatePersonalizer,
        ILogger<EddApprovedEventHandler> logger)
    {
        _notificationService = notificationService;
        _templatePersonalizer = templatePersonalizer;
        _logger = logger;
    }

    public async Task HandleAsync(EddApprovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing EDD approval notification: ClientId={ClientId}, RiskLevel={RiskLevel}, CorrelationId={CorrelationId}",
            domainEvent.ClientId, domainEvent.RiskAcceptanceLevel, domainEvent.CorrelationId);

        try
        {
            // Build personalization data
            var personalizationData = _templatePersonalizer.BuildEddApprovedData(
                clientName: domainEvent.ClientName,
                approvalDate: domainEvent.ApprovedAt,
                riskAcceptanceLevel: domainEvent.RiskAcceptanceLevel,
                nextSteps: "Your loan application will proceed");

            // Send notification
            var result = await _notificationService.SendKycStatusNotificationAsync(
                clientId: domainEvent.ClientId,
                templateId: "edd_approved",
                personalizations: personalizationData,
                correlationId: domainEvent.CorrelationId);

            if (result.IsSuccess && result.Value!.Success)
            {
                _logger.LogInformation(
                    "EDD approval notification sent successfully: ClientId={ClientId}",
                    domainEvent.ClientId);
            }
            else if (result.IsSuccess && !string.IsNullOrEmpty(result.Value!.BlockedReason))
            {
                _logger.LogInformation(
                    "EDD approval notification blocked: ClientId={ClientId}, Reason={Reason}",
                    domainEvent.ClientId, result.Value.BlockedReason);
            }
            else
            {
                _logger.LogWarning(
                    "EDD approval notification failed: ClientId={ClientId}, Error={Error}",
                    domainEvent.ClientId, result.Error ?? result.Value?.FinalError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling EDD approved event: ClientId={ClientId}",
                domainEvent.ClientId);

            // Don't throw - notification failures should not break the workflow
        }
    }
}
