using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using MassTransit;

namespace IntelliFin.CreditAssessmentService.Services;

/// <summary>
/// Publishes audit trail entries to the AdminService topic via MassTransit.
/// </summary>
public sealed class AuditTrailPublisher : IAuditTrailPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AuditTrailPublisher> _logger;

    public AuditTrailPublisher(IPublishEndpoint publishEndpoint, ILogger<AuditTrailPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync(CreditAssessment assessment, string action, string details, CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new
            {
                AssessmentId = assessment.Id,
                assessment.LoanApplicationId,
                assessment.ClientId,
                assessment.RiskGrade,
                assessment.Decision,
                Action = action,
                Details = details,
                OccurredAt = DateTime.UtcNow,
                Service = "CreditAssessmentService"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish audit trail event for assessment {AssessmentId}", assessment.Id);
        }
    }
}
