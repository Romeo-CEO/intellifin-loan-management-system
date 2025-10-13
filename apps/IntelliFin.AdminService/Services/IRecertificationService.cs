using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IRecertificationService
{
    Task<string> CreateCampaignAsync(
        string campaignId,
        string campaignName,
        int quarter,
        int year,
        DateTime startDate,
        DateTime dueDate,
        CancellationToken cancellationToken);

    Task<List<RecertificationCampaignSummaryDto>> GetCampaignsAsync(string? status, CancellationToken cancellationToken);
    Task<List<RecertificationTaskDto>> GetManagerTasksAsync(string managerId, CancellationToken cancellationToken);
    Task<List<RecertificationUserReviewDto>> GetTaskUsersAsync(Guid taskId, CancellationToken cancellationToken);
    Task SubmitReviewDecisionAsync(RecertificationReviewDecisionDto decision, string managerId, string managerName, CancellationToken cancellationToken);
    Task<BulkApprovalResult> BulkApproveAsync(Guid taskId, IReadOnlyCollection<string> userIds, string managerId, CancellationToken cancellationToken);
    Task<int> ProcessTaskRemindersAsync(DateTime utcNow, CancellationToken cancellationToken);
    Task CompleteTaskAsync(Guid taskId, string managerId, CancellationToken cancellationToken);
    Task<RecertificationReportDto> GenerateReportAsync(string campaignId, string reportType, string userId, CancellationToken cancellationToken);
    Task<RecertificationReport?> GetReportAsync(Guid reportId, string accessedBy, CancellationToken cancellationToken);
    Task<CampaignStatisticsDto> GetCampaignStatisticsAsync(string campaignId, CancellationToken cancellationToken);
}

public sealed record BulkApprovalResult(int ApprovedCount, int FailedCount);
