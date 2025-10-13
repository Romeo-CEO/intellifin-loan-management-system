using System;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IRecertificationNotificationService
{
    Task NotifyCampaignLaunchedAsync(RecertificationCampaign campaign, CancellationToken cancellationToken);
    Task NotifyManagerTaskAssignedAsync(RecertificationTask task, CancellationToken cancellationToken);
    Task NotifyManagerReminderAsync(RecertificationTask task, int reminderNumber, CancellationToken cancellationToken);
    Task NotifyEscalationAsync(RecertificationTask task, string escalationTargetId, CancellationToken cancellationToken);
    Task NotifyAccessDecisionAsync(RecertificationReview review, string decision, CancellationToken cancellationToken);
    Task NotifyCampaignCompletedAsync(RecertificationCampaign campaign, CancellationToken cancellationToken);
}
