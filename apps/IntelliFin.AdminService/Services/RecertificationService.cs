using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class RecertificationService : IRecertificationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    static RecertificationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly AdminDbContext _dbContext;
    private readonly ICamundaWorkflowService _camundaWorkflowService;
    private readonly IManagerDirectoryService _directoryService;
    private readonly IRecertificationNotificationService _notificationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<RecertificationService> _logger;

    public RecertificationService(
        AdminDbContext dbContext,
        ICamundaWorkflowService camundaWorkflowService,
        IManagerDirectoryService directoryService,
        IRecertificationNotificationService notificationService,
        IAuditService auditService,
        ILogger<RecertificationService> logger)
    {
        _dbContext = dbContext;
        _camundaWorkflowService = camundaWorkflowService;
        _directoryService = directoryService;
        _notificationService = notificationService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<string> CreateCampaignAsync(
        string campaignId,
        string campaignName,
        int quarter,
        int year,
        DateTime startDate,
        DateTime dueDate,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.RecertificationCampaigns
            .FirstOrDefaultAsync(c => c.CampaignId == campaignId, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation("Recertification campaign {CampaignId} already exists", campaignId);
            return existing.CampaignId;
        }

        var assignments = await _directoryService.GetManagerUserAssignmentsAsync(cancellationToken);

        var campaign = new RecertificationCampaign
        {
            CampaignId = campaignId,
            CampaignName = campaignName,
            Quarter = quarter,
            Year = year,
            StartDate = startDate,
            DueDate = dueDate,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SYSTEM"
        };

        _dbContext.RecertificationCampaigns.Add(campaign);

        var groupedAssignments = assignments
            .GroupBy(a => a.ManagerUserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var createdTasks = new List<RecertificationTask>();

        foreach (var group in groupedAssignments)
        {
            var sample = group.First();
            var task = new RecertificationTask
            {
                TaskId = Guid.NewGuid(),
                CampaignId = campaignId,
                ManagerUserId = group.Key,
                ManagerName = sample.ManagerName,
                ManagerEmail = sample.ManagerEmail,
                AssignedAt = DateTime.UtcNow,
                DueDate = dueDate,
                Status = "Pending",
                UsersInScope = group.Count()
            };

            _dbContext.RecertificationTasks.Add(task);
            createdTasks.Add(task);

            foreach (var assignment in group)
            {
                var review = new RecertificationReview
                {
                    ReviewId = Guid.NewGuid(),
                    TaskId = task.TaskId,
                    CampaignId = campaignId,
                    UserId = assignment.UserId,
                    UserName = assignment.UserName,
                    UserEmail = assignment.UserEmail,
                    UserDepartment = assignment.Department,
                    UserJobTitle = assignment.JobTitle,
                    CurrentRoles = SerializeCollection(assignment.Roles),
                    CurrentPermissions = SerializeCollection(assignment.Permissions),
                    RiskLevel = assignment.RiskLevel,
                    RiskIndicators = SerializeCollection(assignment.RiskIndicators),
                    LastLoginDate = assignment.LastLoginUtc,
                    AccessGrantedDate = assignment.AccessGrantedUtc,
                    Decision = "Pending"
                };

                _dbContext.RecertificationReviews.Add(review);
            }

            campaign.TotalUsersInScope += task.UsersInScope;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyCampaignLaunchedAsync(campaign, cancellationToken);

        foreach (var task in createdTasks)
        {
            await _notificationService.NotifyManagerTaskAssignedAsync(task, cancellationToken);
        }

        await _auditService.LogEventAsync(new AuditEvent
        {
            Actor = "SYSTEM",
            Action = "RecertificationCampaignCreated",
            EntityType = "RecertificationCampaign",
            EntityId = campaign.CampaignId,
            EventData = JsonSerializer.Serialize(new
            {
                campaign.CampaignName,
                campaign.StartDate,
                campaign.DueDate,
                campaign.TotalUsersInScope,
                ManagerTasks = createdTasks.Count
            })
        }, cancellationToken);

        var processInstanceId = await _camundaWorkflowService.StartRecertificationCampaignAsync(campaignId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(processInstanceId))
        {
            campaign.CamundaProcessInstanceId = processInstanceId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Created recertification campaign {CampaignId} with {UserCount} users", campaignId, campaign.TotalUsersInScope);
        return campaignId;
    }

    public async Task<List<RecertificationCampaignSummaryDto>> GetCampaignsAsync(string? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.RecertificationCampaigns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            query = query.Where(c => c.Status == normalized);
        }

        var campaigns = await query
            .OrderByDescending(c => c.StartDate)
            .Select(c => new RecertificationCampaignSummaryDto
            {
                CampaignId = c.CampaignId,
                CampaignName = c.CampaignName,
                StartDate = c.StartDate,
                DueDate = c.DueDate,
                Status = c.Status,
                TotalUsersInScope = c.TotalUsersInScope,
                UsersReviewed = c.UsersReviewed,
                UsersApproved = c.UsersApproved,
                UsersRevoked = c.UsersRevoked,
                UsersModified = c.UsersModified,
                CompletionPercentage = c.TotalUsersInScope == 0
                    ? 0m
                    : Math.Round((decimal)c.UsersReviewed / c.TotalUsersInScope * 100m, 2),
                EscalationCount = c.EscalationCount
            })
            .ToListAsync(cancellationToken);

        return campaigns;
    }

    public async Task<List<RecertificationTaskDto>> GetManagerTasksAsync(string managerId, CancellationToken cancellationToken)
    {
        var tasks = await (from task in _dbContext.RecertificationTasks.AsNoTracking()
                           join campaign in _dbContext.RecertificationCampaigns.AsNoTracking()
                               on task.CampaignId equals campaign.CampaignId
                           where task.ManagerUserId == managerId
                           orderby task.DueDate
                           select new RecertificationTaskDto
                           {
                               TaskId = task.TaskId,
                               CampaignId = task.CampaignId,
                               CampaignName = campaign.CampaignName,
                               Status = task.Status,
                               DueDate = task.DueDate,
                               UsersInScope = task.UsersInScope,
                               UsersReviewed = task.UsersReviewed,
                               RemindersSent = task.RemindersSent,
                               LastReminderAt = task.LastReminderAt,
                               EscalatedTo = task.EscalatedTo,
                               EscalatedAt = task.EscalatedAt
                           })
            .ToListAsync(cancellationToken);

        return tasks;
    }

    public async Task<List<RecertificationUserReviewDto>> GetTaskUsersAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _dbContext.RecertificationTasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TaskId == taskId, cancellationToken);
        if (task == null)
        {
            throw new KeyNotFoundException($"Recertification task {taskId} not found");
        }

        var reviews = await _dbContext.RecertificationReviews.AsNoTracking()
            .Where(r => r.TaskId == taskId)
            .OrderByDescending(r => r.RiskLevel)
            .ThenBy(r => r.UserName)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => new RecertificationUserReviewDto
        {
            ReviewId = r.ReviewId,
            UserId = r.UserId,
            UserName = r.UserName,
            UserEmail = r.UserEmail,
            Department = r.UserDepartment,
            JobTitle = r.UserJobTitle,
            CurrentRoles = DeserializeCollection(r.CurrentRoles),
            CurrentPermissions = DeserializeCollection(r.CurrentPermissions),
            LastLoginDate = r.LastLoginDate,
            AccessGrantedDate = r.AccessGrantedDate,
            RiskLevel = r.RiskLevel,
            RiskIndicators = DeserializeCollection(r.RiskIndicators),
            Decision = r.Decision,
            DecisionComments = r.DecisionComments,
            DecisionMadeAt = r.DecisionMadeAt
        }).ToList();
    }

    public async Task SubmitReviewDecisionAsync(RecertificationReviewDecisionDto decision, string managerId, string managerName, CancellationToken cancellationToken)
    {
        if (decision == null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        if (string.IsNullOrWhiteSpace(decision.Decision))
        {
            throw new ValidationException("Decision is required");
        }

        var review = await _dbContext.RecertificationReviews
            .FirstOrDefaultAsync(r => r.ReviewId == decision.ReviewId, cancellationToken);
        if (review == null)
        {
            throw new KeyNotFoundException($"Review {decision.ReviewId} not found");
        }

        var task = await _dbContext.RecertificationTasks
            .FirstOrDefaultAsync(t => t.TaskId == review.TaskId, cancellationToken);
        if (task == null)
        {
            throw new KeyNotFoundException($"Task {review.TaskId} not found");
        }

        if (!string.Equals(task.ManagerUserId, managerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Task is assigned to a different manager");
        }

        var normalizedDecision = decision.Decision.Trim();
        if (normalizedDecision.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            normalizedDecision = "Approved";
        }
        else if (normalizedDecision.Equals("Revoked", StringComparison.OrdinalIgnoreCase))
        {
            normalizedDecision = "Revoked";
        }
        else if (normalizedDecision.Equals("Modified", StringComparison.OrdinalIgnoreCase))
        {
            normalizedDecision = "Modified";
        }
        else if (normalizedDecision.Equals("Pending", StringComparison.OrdinalIgnoreCase))
        {
            normalizedDecision = "Pending";
        }
        else
        {
            throw new ValidationException($"Unsupported decision '{decision.Decision}'");
        }

        if (normalizedDecision is "Revoked" or "Modified")
        {
            if (string.IsNullOrWhiteSpace(decision.Comments) || decision.Comments!.Trim().Length < 20)
            {
                throw new ValidationException("Comments must contain at least 20 characters for revoke/modify decisions");
            }
        }

        var previousDecision = NormalizeDecision(review.Decision);

        review.Decision = normalizedDecision;
        review.DecisionComments = decision.Comments;
        review.DecisionMadeBy = managerId;
        review.DecisionMadeAt = DateTime.UtcNow;
        review.EffectiveDate = decision.EffectiveDate ?? DateTime.UtcNow.AddDays(30);
        review.RolesToRevoke = decision.RolesToRevoke is { Count: > 0 }
            ? JsonSerializer.Serialize(decision.RolesToRevoke, SerializerOptions)
            : null;

        if (!string.Equals(normalizedDecision, "Modified", StringComparison.OrdinalIgnoreCase))
        {
            review.RolesToRevoke = null;
        }

        var campaign = await _dbContext.RecertificationCampaigns
            .FirstOrDefaultAsync(c => c.CampaignId == review.CampaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"Campaign {review.CampaignId} not found");

        UpdateCounters(task, campaign, previousDecision, normalizedDecision);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyAccessDecisionAsync(review, normalizedDecision, cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Actor = managerId,
            Action = $"AccessRecertification{normalizedDecision}",
            EntityType = "RecertificationReview",
            EntityId = review.ReviewId.ToString(),
            EventData = JsonSerializer.Serialize(new
            {
                review.UserId,
                review.CampaignId,
                Decision = normalizedDecision,
                review.DecisionComments,
                review.EffectiveDate
            })
        }, cancellationToken);

        _logger.LogInformation(
            "Manager {ManagerId} submitted decision {Decision} for user {UserId} in campaign {CampaignId}",
            managerId,
            normalizedDecision,
            review.UserId,
            review.CampaignId);
    }

    public async Task<BulkApprovalResult> BulkApproveAsync(Guid taskId, IReadOnlyCollection<string> userIds, string managerId, CancellationToken cancellationToken)
    {
        if (userIds == null || userIds.Count == 0)
        {
            return new BulkApprovalResult(0, 0);
        }

        var task = await _dbContext.RecertificationTasks
            .FirstOrDefaultAsync(t => t.TaskId == taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (!string.Equals(task.ManagerUserId, managerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Task is assigned to a different manager");
        }

        var approved = 0;
        var failed = 0;

        foreach (var userId in userIds)
        {
            try
            {
                var review = await _dbContext.RecertificationReviews
                    .FirstOrDefaultAsync(r => r.TaskId == taskId && r.UserId == userId, cancellationToken);

                if (review == null)
                {
                    failed++;
                    continue;
                }

                if (string.Equals(review.RiskLevel, "High", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(review.RiskLevel, "Critical", StringComparison.OrdinalIgnoreCase))
                {
                    failed++;
                    continue;
                }

                await SubmitReviewDecisionAsync(
                    new RecertificationReviewDecisionDto
                    {
                        ReviewId = review.ReviewId,
                        Decision = "Approved",
                        Comments = "Bulk approval - low risk user"
                    },
                    managerId,
                    task.ManagerName,
                    cancellationToken);

                approved++;
            }
            catch
            {
                failed++;
            }
        }

        return new BulkApprovalResult(approved, failed);
    }

    public async Task<int> ProcessTaskRemindersAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var thresholdDays = new[] { 20, 25, 28 };

        var tasks = await _dbContext.RecertificationTasks
            .Where(t => t.Status == "Pending" || t.Status == "InProgress")
            .ToListAsync(cancellationToken);

        if (tasks.Count == 0)
        {
            return 0;
        }

        var assignments = await _directoryService.GetManagerUserAssignmentsAsync(cancellationToken);
        var campaignIds = tasks
            .Select(t => t.CampaignId)
            .Distinct()
            .ToList();

        var campaignLookup = await _dbContext.RecertificationCampaigns
            .Where(c => campaignIds.Contains(c.CampaignId))
            .ToListAsync(cancellationToken);

        var campaigns = campaignLookup.ToDictionary(c => c.CampaignId, StringComparer.OrdinalIgnoreCase);

        var actions = 0;

        foreach (var task in tasks)
        {
            var daysOpen = (utcNow - task.AssignedAt).TotalDays;

            if (task.RemindersSent < thresholdDays.Length && daysOpen >= thresholdDays[task.RemindersSent])
            {
                task.RemindersSent++;
                task.LastReminderAt = utcNow;
                if (!string.Equals(task.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
                {
                    task.Status = "InProgress";
                }

                await _notificationService.NotifyManagerReminderAsync(task, task.RemindersSent, cancellationToken);

                await _auditService.LogEventAsync(new AuditEvent
                {
                    Actor = "SYSTEM",
                    Action = "RecertificationReminderSent",
                    EntityType = "RecertificationTask",
                    EntityId = task.TaskId.ToString(),
                    EventData = JsonSerializer.Serialize(new
                    {
                        task.ManagerUserId,
                        ReminderNumber = task.RemindersSent,
                        task.CampaignId
                    })
                }, cancellationToken);

                actions++;
            }

            if (utcNow.Date <= task.DueDate.Date || string.Equals(task.Status, "Escalated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!campaigns.TryGetValue(task.CampaignId, out var campaign))
            {
                continue;
            }

            var escalationTarget = ResolveEscalationTarget(task.ManagerUserId, assignments);

            task.Status = "Escalated";
            task.EscalatedAt = utcNow;
            task.EscalatedTo = escalationTarget?.UserId;

            campaign.EscalationCount++;

            var escalation = new RecertificationEscalation
            {
                EscalationId = Guid.NewGuid(),
                TaskId = task.TaskId,
                CampaignId = task.CampaignId,
                OriginalManagerUserId = task.ManagerUserId,
                EscalatedToUserId = escalationTarget?.UserId ?? "compliance-team",
                EscalationType = escalationTarget?.Type ?? "ComplianceOfficerEscalation",
                EscalatedAt = utcNow
            };

            _dbContext.RecertificationEscalations.Add(escalation);

            await _notificationService.NotifyEscalationAsync(task, escalation.EscalatedToUserId, cancellationToken);

            await _auditService.LogEventAsync(new AuditEvent
            {
                Actor = "SYSTEM",
                Action = "RecertificationTaskEscalated",
                EntityType = "RecertificationTask",
                EntityId = task.TaskId.ToString(),
                EventData = JsonSerializer.Serialize(new
                {
                    task.CampaignId,
                    escalation.EscalatedToUserId,
                    task.ManagerUserId
                })
            }, cancellationToken);

            actions++;
        }

        if (actions > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return actions;
    }

    public async Task CompleteTaskAsync(Guid taskId, string managerId, CancellationToken cancellationToken)
    {
        var task = await _dbContext.RecertificationTasks
            .FirstOrDefaultAsync(t => t.TaskId == taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        if (!string.Equals(task.ManagerUserId, managerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Task is assigned to a different manager");
        }

        var pendingReviews = await _dbContext.RecertificationReviews
            .CountAsync(r => r.TaskId == taskId && r.Decision == "Pending", cancellationToken);

        if (pendingReviews > 0)
        {
            throw new ValidationException($"{pendingReviews} users still require review");
        }

        task.Status = "Completed";
        task.CompletedAt = DateTime.UtcNow;

        await _camundaWorkflowService.CompleteRecertificationTaskAsync(task.CamundaTaskId, task.TaskId, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var campaign = await _dbContext.RecertificationCampaigns
            .FirstOrDefaultAsync(c => c.CampaignId == task.CampaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"Campaign {task.CampaignId} not found");

        await _auditService.LogEventAsync(new AuditEvent
        {
            Actor = managerId,
            Action = "RecertificationTaskCompleted",
            EntityType = "RecertificationTask",
            EntityId = task.TaskId.ToString(),
            EventData = JsonSerializer.Serialize(new
            {
                task.CampaignId,
                task.ManagerUserId,
                task.UsersInScope
            })
        }, cancellationToken);

        var remaining = await _dbContext.RecertificationTasks
            .AnyAsync(t => t.CampaignId == task.CampaignId && t.Status != "Completed", cancellationToken);

        if (!remaining)
        {
            campaign.Status = "Completed";
            campaign.CompletionDate = DateTime.UtcNow;
            campaign.CompletedBy = managerId;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _notificationService.NotifyCampaignCompletedAsync(campaign, cancellationToken);
        }
    }

    public async Task<RecertificationReportDto> GenerateReportAsync(string campaignId, string reportType, string userId, CancellationToken cancellationToken)
    {
        var campaign = await _dbContext.RecertificationCampaigns
            .FirstOrDefaultAsync(c => c.CampaignId == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        var directory = Path.Combine(Path.GetTempPath(), "intellifin-rec", "reports");
        Directory.CreateDirectory(directory);

        var fileName = $"{campaign.CampaignId}_{reportType}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var filePath = Path.Combine(directory, fileName);

        var reviews = await _dbContext.RecertificationReviews.AsNoTracking()
            .Where(r => r.CampaignId == campaignId)
            .ToListAsync(cancellationToken);
        var tasks = await _dbContext.RecertificationTasks.AsNoTracking()
            .Where(t => t.CampaignId == campaignId)
            .ToListAsync(cancellationToken);

        var riskBreakdown = reviews
            .GroupBy(r => string.IsNullOrWhiteSpace(r.RiskLevel) ? "Unspecified" : r.RiskLevel!)
            .Select(group => new { RiskLevel = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.RiskLevel)
            .ToList();

        var overdueTasks = tasks.Where(t => string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase)).Count();
        var escalatedTasks = tasks.Where(t => string.Equals(t.Status, "Escalated", StringComparison.OrdinalIgnoreCase)).Count();

        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Column(header =>
                {
                    header.Item().Text("IntelliFin Quarterly Access Recertification Report").FontSize(20).SemiBold();
                    header.Item().Text($"Campaign: {campaign.CampaignName} ({campaign.CampaignId})").FontSize(12);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(summary =>
                        {
                            summary.Spacing(4);
                            summary.Item().Text($"Status: {campaign.Status}");
                            summary.Item().Text($"Start Date: {campaign.StartDate:yyyy-MM-dd}");
                            summary.Item().Text($"Due Date: {campaign.DueDate:yyyy-MM-dd}");
                            summary.Item().Text($"Completion: {campaign.UsersReviewed}/{campaign.TotalUsersInScope}");
                        });

                        row.RelativeItem().Column(summary =>
                        {
                            summary.Spacing(4);
                            summary.Item().Text($"Approved: {campaign.UsersApproved}");
                            summary.Item().Text($"Revoked: {campaign.UsersRevoked}");
                            summary.Item().Text($"Modified: {campaign.UsersModified}");
                            summary.Item().Text($"Escalations: {campaign.EscalationCount}");
                        });
                    });

                    col.Item().Text("Risk Breakdown").FontSize(14).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Risk Level");
                            header.Cell().Element(CellStyle).AlignRight().Text("Users");
                        });

                        foreach (var risk in riskBreakdown)
                        {
                            table.Cell().Element(CellStyle).Text(risk.RiskLevel);
                            table.Cell().Element(CellStyle).AlignRight().Text(risk.Count.ToString());
                        }
                    });

                    col.Item().Text("Operational Metrics").FontSize(14).SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Element(CellStyle).Text("Manager Tasks");
                        table.Cell().Element(CellStyle).AlignRight().Text(tasks.Count.ToString());

                        table.Cell().Element(CellStyle).Text("Completed Tasks");
                        table.Cell().Element(CellStyle).AlignRight().Text(tasks.Count(t => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase)).ToString());

                        table.Cell().Element(CellStyle).Text("Overdue Tasks");
                        table.Cell().Element(CellStyle).AlignRight().Text(overdueTasks.ToString());

                        table.Cell().Element(CellStyle).Text("Escalated Tasks");
                        table.Cell().Element(CellStyle).AlignRight().Text(escalatedTasks.ToString());
                    });

                    col.Item().Text("Report generated on: " + DateTime.UtcNow.ToString("u"));
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });

            static IContainer CellStyle(IContainer container)
            {
                return container.Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
            }
        }).GeneratePdf(filePath);

        var fileInfo = new FileInfo(filePath);

        var report = new RecertificationReport
        {
            ReportId = Guid.NewGuid(),
            CampaignId = campaignId,
            ReportType = reportType,
            ReportFormat = "PDF",
            FilePath = filePath,
            FileSize = fileInfo.Exists ? fileInfo.Length : 0,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = userId,
            RetentionDate = campaign.StartDate.AddYears(7)
        };

        _dbContext.RecertificationReports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Actor = userId,
            Action = "RecertificationReportGenerated",
            EntityType = "RecertificationCampaign",
            EntityId = campaignId,
            EventData = JsonSerializer.Serialize(new
            {
                report.ReportId,
                report.ReportType,
                report.FileSize
            })
        }, cancellationToken);

        return new RecertificationReportDto
        {
            ReportId = report.ReportId,
            CampaignId = report.CampaignId,
            ReportType = report.ReportType,
            ReportFormat = report.ReportFormat,
            GeneratedAt = report.GeneratedAt,
            FilePath = report.FilePath
        };
    }

    public async Task<RecertificationReport?> GetReportAsync(Guid reportId, string accessedBy, CancellationToken cancellationToken)
    {
        var report = await _dbContext.RecertificationReports
            .FirstOrDefaultAsync(r => r.ReportId == reportId, cancellationToken);

        if (report == null)
        {
            return null;
        }

        report.AccessedCount++;
        report.LastAccessedAt = DateTime.UtcNow;
        report.LastAccessedBy = accessedBy;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<CampaignStatisticsDto> GetCampaignStatisticsAsync(string campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _dbContext.RecertificationCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampaignId == campaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        var managerTaskCount = await _dbContext.RecertificationTasks
            .CountAsync(t => t.CampaignId == campaignId, cancellationToken);
        var completedTaskCount = await _dbContext.RecertificationTasks
            .CountAsync(t => t.CampaignId == campaignId && t.Status == "Completed", cancellationToken);
        var overdueTaskCount = await _dbContext.RecertificationTasks
            .CountAsync(t => t.CampaignId == campaignId && t.Status == "Overdue", cancellationToken);

        return new CampaignStatisticsDto
        {
            CampaignId = campaign.CampaignId,
            TotalUsersInScope = campaign.TotalUsersInScope,
            UsersReviewed = campaign.UsersReviewed,
            UsersApproved = campaign.UsersApproved,
            UsersRevoked = campaign.UsersRevoked,
            UsersModified = campaign.UsersModified,
            ManagerTaskCount = managerTaskCount,
            CompletedTaskCount = completedTaskCount,
            OverdueTaskCount = overdueTaskCount,
            EscalationCount = campaign.EscalationCount,
            CompletionPercentage = campaign.TotalUsersInScope == 0
                ? 0m
                : Math.Round((decimal)campaign.UsersReviewed / campaign.TotalUsersInScope * 100m, 2)
        };
    }

    private static string? SerializeCollection(IReadOnlyCollection<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(values, SerializerOptions);
    }

    private static string NormalizeDecision(string? decision)
    {
        return decision?.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "Approved",
            "REVOKED" => "Revoked",
            "MODIFIED" => "Modified",
            _ => "Pending"
        };
    }

    private static void UpdateCounters(
        RecertificationTask task,
        RecertificationCampaign campaign,
        string previousDecision,
        string currentDecision)
    {
        var previous = NormalizeDecision(previousDecision);
        var current = NormalizeDecision(currentDecision);

        if (string.Equals(previous, current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(previous, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            task.UsersReviewed = Math.Max(0, task.UsersReviewed - 1);
            campaign.UsersReviewed = Math.Max(0, campaign.UsersReviewed - 1);
            DecrementCampaignCounter(campaign, previous);
        }

        if (!string.Equals(current, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            task.UsersReviewed++;
            campaign.UsersReviewed++;
            IncrementCampaignCounter(campaign, current);
        }
    }

    private static void IncrementCampaignCounter(RecertificationCampaign campaign, string decision)
    {
        switch (decision)
        {
            case "Approved":
                campaign.UsersApproved++;
                break;
            case "Revoked":
                campaign.UsersRevoked++;
                break;
            case "Modified":
                campaign.UsersModified++;
                break;
        }
    }

    private static void DecrementCampaignCounter(RecertificationCampaign campaign, string decision)
    {
        switch (decision)
        {
            case "Approved":
                campaign.UsersApproved = Math.Max(0, campaign.UsersApproved - 1);
                break;
            case "Revoked":
                campaign.UsersRevoked = Math.Max(0, campaign.UsersRevoked - 1);
                break;
            case "Modified":
                campaign.UsersModified = Math.Max(0, campaign.UsersModified - 1);
                break;
        }
    }

    private static IReadOnlyList<string> DeserializeCollection(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static EscalationTarget? ResolveEscalationTarget(
        string managerUserId,
        IReadOnlyList<ManagerUserAssignment> assignments)
    {
        if (string.IsNullOrWhiteSpace(managerUserId) || assignments.Count == 0)
        {
            return null;
        }

        var managerRecord = assignments.FirstOrDefault(a =>
            string.Equals(a.UserId, managerUserId, StringComparison.OrdinalIgnoreCase));

        if (managerRecord is not null &&
            !string.IsNullOrWhiteSpace(managerRecord.ManagerUserId) &&
            !string.Equals(managerRecord.ManagerUserId, managerUserId, StringComparison.OrdinalIgnoreCase))
        {
            return new EscalationTarget(managerRecord.ManagerUserId, "ManagerManagerEscalation");
        }

        return null;
    }

    private sealed record EscalationTarget(string UserId, string Type);

    private async Task RecalculateTaskAndCampaignAsync(RecertificationTask task, RecertificationCampaign campaign, CancellationToken cancellationToken)
    {
        var taskDecisions = await _dbContext.RecertificationReviews
            .Where(r => r.TaskId == task.TaskId)
            .Select(r => r.Decision)
            .ToListAsync(cancellationToken);

        task.UsersReviewed = taskDecisions.Count(d => !string.Equals(d, "Pending", StringComparison.OrdinalIgnoreCase));

        campaign.UsersReviewed = await _dbContext.RecertificationReviews
            .CountAsync(r => r.CampaignId == campaign.CampaignId && r.Decision != "Pending", cancellationToken);
        campaign.UsersApproved = await _dbContext.RecertificationReviews
            .CountAsync(r => r.CampaignId == campaign.CampaignId && r.Decision == "Approved", cancellationToken);
        campaign.UsersRevoked = await _dbContext.RecertificationReviews
            .CountAsync(r => r.CampaignId == campaign.CampaignId && r.Decision == "Revoked", cancellationToken);
        campaign.UsersModified = await _dbContext.RecertificationReviews
            .CountAsync(r => r.CampaignId == campaign.CampaignId && r.Decision == "Modified", cancellationToken);
    }
}
