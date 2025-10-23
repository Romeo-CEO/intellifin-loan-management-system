using IntelliFin.CreditAssessmentService.Services.Interfaces;
using MassTransit;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Messaging;

/// <summary>
/// Listens for KYC change events to invalidate existing assessments when needed.
/// </summary>
public sealed class KycStatusEventHandler : IConsumer<KycStatusChangedEvent>
{
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly ILogger<KycStatusEventHandler> _logger;

    public KycStatusEventHandler(ICreditAssessmentService creditAssessmentService, ILogger<KycStatusEventHandler> logger)
    {
        _creditAssessmentService = creditAssessmentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<KycStatusChangedEvent> context)
    {
        if (context.Message.IsKycComplete)
        {
            return;
        }

        _logger.LogInformation("Received KYC invalidation for client {ClientId}", context.Message.ClientId);

        foreach (var assessmentId in context.Message.AffectedAssessmentIds)
        {
            try
            {
                await _creditAssessmentService.InvalidateAsync(assessmentId, context.Message.Reason, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate assessment {AssessmentId}", assessmentId);
            }
        }
    }
}

/// <summary>
/// Message contract emitted by Client Management when KYC status changes.
/// </summary>
public sealed record KycStatusChangedEvent(Guid ClientId, bool IsKycComplete, string Reason, IReadOnlyCollection<Guid> AffectedAssessmentIds);
