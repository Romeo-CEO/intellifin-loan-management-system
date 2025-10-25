using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace IntelliFin.CreditAssessmentService.EventHandlers;

/// <summary>
/// Handles KYC status change events to invalidate affected assessments.
/// Story 1.12: KYC Status Event Subscription
/// </summary>
public class KycStatusEventHandler :
    IConsumer<KycExpiredEvent>,
    IConsumer<KycRevokedEvent>,
    IConsumer<KycUpdatedEvent>
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<KycStatusEventHandler> _logger;

    public KycStatusEventHandler(LmsDbContext dbContext, ILogger<KycStatusEventHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<KycExpiredEvent> context)
    {
        var @event = context.Message;
        _logger.LogWarning("KYC expired for client {ClientId}, invalidating assessments", @event.ClientId);

        await InvalidateAssessmentsAsync(@event.ClientId, "KYC verification expired", context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<KycRevokedEvent> context)
    {
        var @event = context.Message;
        _logger.LogWarning("KYC revoked for client {ClientId}, invalidating assessments", @event.ClientId);

        await InvalidateAssessmentsAsync(@event.ClientId, $"KYC verification revoked: {@event.Reason}", context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<KycUpdatedEvent> context)
    {
        var @event = context.Message;
        _logger.LogInformation("KYC updated for client {ClientId}, checking for reassessment", @event.ClientId);

        // If KYC was previously invalid and now valid, log for potential reassessment
        // Don't automatically invalidate on update
        await Task.CompletedTask;
    }

    private async Task InvalidateAssessmentsAsync(Guid clientId, string reason, CancellationToken cancellationToken)
    {
        var activeAssessments = await _dbContext.CreditAssessments
            .Include(a => a.LoanApplication)
            .Where(a => a.LoanApplication != null && 
                       a.LoanApplication.ClientId == clientId && 
                       a.IsValid)
            .ToListAsync(cancellationToken);

        foreach (var assessment in activeAssessments)
        {
            assessment.IsValid = false;
            assessment.InvalidReason = reason;
            
            _logger.LogInformation("Invalidated assessment {AssessmentId} for client {ClientId}: {Reason}",
                assessment.Id, clientId, reason);
        }

        if (activeAssessments.Any())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Invalidated {Count} assessments for client {ClientId}", 
                activeAssessments.Count, clientId);
        }
    }
}

// Event DTOs
public record KycExpiredEvent(Guid ClientId, DateTime ExpiredAt);
public record KycRevokedEvent(Guid ClientId, string Reason, DateTime RevokedAt);
public record KycUpdatedEvent(Guid ClientId, string UpdateType, DateTime UpdatedAt);
