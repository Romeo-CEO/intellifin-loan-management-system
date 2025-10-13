using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class RecertificationNotificationService : IRecertificationNotificationService
{
    private readonly IAuditService _auditService;
    private readonly ILogger<RecertificationNotificationService> _logger;

    public RecertificationNotificationService(IAuditService auditService, ILogger<RecertificationNotificationService> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public Task NotifyCampaignLaunchedAsync(RecertificationCampaign campaign, CancellationToken cancellationToken)
        => LogAsync("RecertificationCampaignLaunched", campaign.CampaignId, new
        {
            campaign.CampaignName,
            campaign.StartDate,
            campaign.DueDate,
            campaign.TotalUsersInScope
        }, cancellationToken);

    public Task NotifyManagerTaskAssignedAsync(RecertificationTask task, CancellationToken cancellationToken)
        => LogAsync("RecertificationTaskAssigned", task.CampaignId, new
        {
            task.TaskId,
            task.ManagerUserId,
            task.ManagerEmail,
            task.UsersInScope,
            task.DueDate
        }, cancellationToken);

    public Task NotifyManagerReminderAsync(RecertificationTask task, int reminderNumber, CancellationToken cancellationToken)
        => LogAsync("RecertificationTaskReminder", task.CampaignId, new
        {
            task.TaskId,
            task.ManagerUserId,
            reminderNumber,
            task.DueDate
        }, cancellationToken);

    public Task NotifyEscalationAsync(RecertificationTask task, string escalationTargetId, CancellationToken cancellationToken)
        => LogAsync("RecertificationTaskEscalated", task.CampaignId, new
        {
            task.TaskId,
            task.ManagerUserId,
            escalationTargetId,
            task.DueDate
        }, cancellationToken);

    public Task NotifyAccessDecisionAsync(RecertificationReview review, string decision, CancellationToken cancellationToken)
        => LogAsync("RecertificationDecisionNotified", review.CampaignId, new
        {
            review.ReviewId,
            review.UserId,
            decision,
            review.DecisionComments,
            review.EffectiveDate
        }, cancellationToken);

    public Task NotifyCampaignCompletedAsync(RecertificationCampaign campaign, CancellationToken cancellationToken)
        => LogAsync("RecertificationCampaignCompleted", campaign.CampaignId, new
        {
            campaign.CampaignName,
            campaign.UsersReviewed,
            campaign.UsersApproved,
            campaign.UsersRevoked,
            campaign.UsersModified,
            campaign.CompletionDate
        }, cancellationToken);

    private async Task LogAsync(string action, string campaignId, object payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recertification notification {Action} for campaign {CampaignId}: {Payload}",
            action,
            campaignId,
            JsonSerializer.Serialize(payload));

        await _auditService.LogEventAsync(new AuditEvent
        {
            Actor = "SYSTEM",
            Action = action,
            EntityType = "RecertificationCampaign",
            EntityId = campaignId,
            EventData = JsonSerializer.Serialize(payload)
        }, cancellationToken);
    }
}
