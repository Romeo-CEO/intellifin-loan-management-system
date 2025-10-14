using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class ConfigurationManagementService : IConfigurationManagementService
{
    private readonly AdminDbContext _dbContext;
    private readonly IConfigurationDeployer _deployer;
    private readonly ICamundaWorkflowService _camundaWorkflowService;
    private readonly IAuditService _auditService;
    private readonly IOptionsMonitor<ConfigurationManagementOptions> _optionsMonitor;
    private readonly ILogger<ConfigurationManagementService> _logger;

    public ConfigurationManagementService(
        AdminDbContext dbContext,
        IConfigurationDeployer deployer,
        ICamundaWorkflowService camundaWorkflowService,
        IAuditService auditService,
        IOptionsMonitor<ConfigurationManagementOptions> optionsMonitor,
        ILogger<ConfigurationManagementService> logger)
    {
        _dbContext = dbContext;
        _deployer = deployer;
        _camundaWorkflowService = camundaWorkflowService;
        _auditService = auditService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ConfigurationPolicyDto>> GetPoliciesAsync(string? category, CancellationToken cancellationToken)
    {
        var query = _dbContext.ConfigurationPolicies.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        var policies = await query
            .OrderBy(p => p.Category)
            .ThenBy(p => p.ConfigKey)
            .Select(p => new ConfigurationPolicyDto
            {
                Id = p.Id,
                ConfigKey = p.ConfigKey,
                Category = p.Category,
                RequiresApproval = p.RequiresApproval,
                Sensitivity = p.Sensitivity,
                Description = p.Description,
                CurrentValue = RedactValue(p, p.CurrentValue)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return policies;
    }

    public async Task<IReadOnlyCollection<ConfigurationValueDto>> GetCurrentValuesAsync(string? category, CancellationToken cancellationToken)
    {
        var policies = await GetPoliciesAsync(category, cancellationToken).ConfigureAwait(false);
        return policies
            .Select(p => new ConfigurationValueDto
            {
                ConfigKey = p.ConfigKey,
                CurrentValue = p.CurrentValue,
                Sensitivity = p.Sensitivity,
                RequiresApproval = p.RequiresApproval
            })
            .ToList();
    }

    public async Task<ConfigChangeResponse> RequestChangeAsync(ConfigChangeRequest request, string requestorId, string requestorName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length < 20)
        {
            throw new ValidationException("Justification must be at least 20 characters");
        }

        var policy = await _dbContext.ConfigurationPolicies
            .FirstOrDefaultAsync(p => p.ConfigKey == request.ConfigKey, cancellationToken)
            .ConfigureAwait(false);

        if (policy is null)
        {
            throw new ValidationException($"Configuration key '{request.ConfigKey}' not found");
        }

        ValidateValue(policy, request.NewValue);

        var correlationId = Guid.NewGuid().ToString("N");
        var changeRequestId = Guid.NewGuid();
        var currentValue = await _deployer.GetCurrentValueAsync(policy, cancellationToken).ConfigureAwait(false) ?? policy.CurrentValue;
        var options = _optionsMonitor.CurrentValue;

        var change = new ConfigurationChange
        {
            ChangeRequestId = changeRequestId,
            ConfigKey = policy.ConfigKey,
            OldValue = currentValue,
            NewValue = request.NewValue,
            Justification = request.Justification,
            Category = string.IsNullOrWhiteSpace(request.Category) ? policy.Category : request.Category!,
            Status = policy.RequiresApproval ? "Pending" : "Applied",
            Sensitivity = policy.Sensitivity,
            RequestedBy = requestorId,
            RequestedAt = DateTime.UtcNow,
            GitRepository = options.GitRepository,
            GitBranch = options.GitBranch,
            KubernetesNamespace = policy.KubernetesNamespace,
            KubernetesConfigMap = policy.KubernetesConfigMap,
            ConfigMapKey = policy.ConfigMapKey,
            CorrelationId = correlationId
        };

        _dbContext.ConfigurationChanges.Add(change);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(new AuditEvent
        {
            Actor = requestorId,
            Action = "ConfigChangeRequested",
            EntityType = "ConfigurationChange",
            EntityId = changeRequestId.ToString(),
            CorrelationId = correlationId,
            EventData = JsonSerializer.Serialize(new
            {
                change.ConfigKey,
                oldValue = RedactValue(policy, currentValue),
                newValue = RedactValue(policy, request.NewValue),
                change.Justification,
                change.Category,
                change.Sensitivity
            })
        }, cancellationToken).ConfigureAwait(false);

        if (policy.RequiresApproval)
        {
            var processInstanceId = await _camundaWorkflowService.StartConfigurationChangeWorkflowAsync(change, policy, requestorName, cancellationToken).ConfigureAwait(false);
            change.CamundaProcessInstanceId = processInstanceId;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new ConfigChangeResponse
            {
                ChangeRequestId = changeRequestId,
                Status = change.Status,
                RequiresApproval = true,
                Message = "Configuration change request submitted. Awaiting approval.",
                EstimatedApprovalTime = DateTime.UtcNow.AddHours(24)
            };
        }

        await ApplyChangeAsync(change, policy, requestorId, cancellationToken).ConfigureAwait(false);

        return new ConfigChangeResponse
        {
            ChangeRequestId = changeRequestId,
            Status = change.Status,
            RequiresApproval = false,
            Message = "Configuration change applied successfully."
        };
    }

    public async Task<ConfigChangeStatusDto?> GetChangeRequestStatusAsync(Guid changeRequestId, CancellationToken cancellationToken)
    {
        var change = await _dbContext.ConfigurationChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChangeRequestId == changeRequestId, cancellationToken)
            .ConfigureAwait(false);

        return change is null
            ? null
            : new ConfigChangeStatusDto
            {
                ChangeRequestId = change.ChangeRequestId,
                ConfigKey = change.ConfigKey,
                Status = change.Status,
                Sensitivity = change.Sensitivity,
                ApprovedBy = change.ApprovedBy,
                ApprovedAt = change.ApprovedAt,
                AppliedAt = change.AppliedAt,
                GitCommitSha = change.GitCommitSha,
                CamundaProcessInstanceId = change.CamundaProcessInstanceId,
                RequestedBy = change.RequestedBy,
                RequestedAt = change.RequestedAt
            };
    }

    public async Task<PagedResult<ConfigChangeSummaryDto>> ListChangeRequestsAsync(string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 10, 200);

        var query = _dbContext.ConfigurationChanges.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(c => c.RequestedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(c => new ConfigChangeSummaryDto
            {
                ChangeRequestId = c.ChangeRequestId,
                ConfigKey = c.ConfigKey,
                Status = c.Status,
                RequestedBy = c.RequestedBy,
                RequestedAt = c.RequestedAt,
                Sensitivity = c.Sensitivity,
                Category = c.Category
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ConfigChangeSummaryDto>(items, safePage, safePageSize, total);
    }

    public async Task ApproveChangeAsync(Guid changeRequestId, string approverId, string approverName, string? comments, CancellationToken cancellationToken)
    {
        var change = await _dbContext.ConfigurationChanges
            .FirstOrDefaultAsync(c => c.ChangeRequestId == changeRequestId, cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Configuration change not found");

        if (!string.Equals(change.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"Change request {changeRequestId} is not pending");
        }

        var policy = await _dbContext.ConfigurationPolicies
            .FirstAsync(p => p.ConfigKey == change.ConfigKey, cancellationToken)
            .ConfigureAwait(false);

        change.Status = "Approved";
        change.ApprovedBy = approverId;
        change.ApprovedAt = DateTime.UtcNow;

        await _camundaWorkflowService.CompleteConfigurationChangeWorkflowAsync(change.CamundaProcessInstanceId, approved: true, comments ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await ApplyChangeAsync(change, policy, approverId, cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(new AuditEvent
        {
            Actor = approverId,
            Action = "ConfigChangeApproved",
            EntityType = "ConfigurationChange",
            EntityId = changeRequestId.ToString(),
            CorrelationId = change.CorrelationId,
            EventData = JsonSerializer.Serialize(new
            {
                change.ConfigKey,
                comments,
                change.Sensitivity,
                approvedBy = approverName
            })
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task RejectChangeAsync(Guid changeRequestId, string reviewerId, string reviewerName, string reason, CancellationToken cancellationToken)
    {
        var change = await _dbContext.ConfigurationChanges
            .FirstOrDefaultAsync(c => c.ChangeRequestId == changeRequestId, cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Configuration change not found");

        if (!string.Equals(change.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"Change request {changeRequestId} is not pending");
        }

        change.Status = "Rejected";
        change.RejectedBy = reviewerId;
        change.RejectedAt = DateTime.UtcNow;
        change.RejectionReason = reason;

        await _camundaWorkflowService.CompleteConfigurationChangeWorkflowAsync(change.CamundaProcessInstanceId, approved: false, reason, cancellationToken).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(new AuditEvent
        {
            Actor = reviewerId,
            Action = "ConfigChangeRejected",
            EntityType = "ConfigurationChange",
            EntityId = changeRequestId.ToString(),
            CorrelationId = change.CorrelationId,
            EventData = JsonSerializer.Serialize(new
            {
                change.ConfigKey,
                reason,
                reviewer = reviewerName
            })
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConfigRollbackResponse> RollbackChangeAsync(ConfigRollbackRequest request, string adminId, string adminName, CancellationToken cancellationToken)
    {
        ConfigurationChange? targetChange = null;
        if (request.ChangeRequestId.HasValue)
        {
            targetChange = await _dbContext.ConfigurationChanges
                .FirstOrDefaultAsync(c => c.ChangeRequestId == request.ChangeRequestId, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(request.GitCommitSha))
        {
            targetChange = await _dbContext.ConfigurationChanges
                .OrderByDescending(c => c.RequestedAt)
                .FirstOrDefaultAsync(c => c.GitCommitSha == request.GitCommitSha, cancellationToken)
                .ConfigureAwait(false);
        }

        if (targetChange is null)
        {
            throw new NotFoundException("Original configuration change not found");
        }

        var policy = await _dbContext.ConfigurationPolicies
            .FirstOrDefaultAsync(p => p.ConfigKey == targetChange.ConfigKey, cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Configuration policy not found");

        var rollbackValue = targetChange.OldValue ?? string.Empty;
        var justification = $"Rollback of change {targetChange.ChangeRequestId}. {request.Reason}";
        if (justification.Length < 20)
        {
            justification = justification.PadRight(20, '.');
        }

        var rollbackRequest = new ConfigChangeRequest
        {
            ConfigKey = targetChange.ConfigKey,
            NewValue = rollbackValue,
            Justification = justification,
            Category = targetChange.Category
        };

        var response = await RequestChangeAsync(rollbackRequest, adminId, adminName, cancellationToken).ConfigureAwait(false);

        var rollbackRecord = new ConfigurationRollback
        {
            RollbackId = Guid.NewGuid(),
            OriginalChangeRequestId = targetChange.ChangeRequestId,
            NewChangeRequestId = response.ChangeRequestId,
            ConfigKey = targetChange.ConfigKey,
            RolledBackValue = rollbackValue,
            Reason = request.Reason,
            RolledBackBy = adminId,
            RolledBackAt = DateTime.UtcNow
        };

        _dbContext.ConfigurationRollbacks.Add(rollbackRecord);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(new AuditEvent
        {
            Actor = adminId,
            Action = "ConfigChangeRolledBack",
            EntityType = "ConfigurationRollback",
            EntityId = rollbackRecord.RollbackId.ToString(),
            CorrelationId = targetChange.CorrelationId,
            EventData = JsonSerializer.Serialize(new
            {
                originalChange = targetChange.ChangeRequestId,
                newChange = response.ChangeRequestId,
                policy.ConfigKey,
                reason = request.Reason
            })
        }, cancellationToken).ConfigureAwait(false);

        return new ConfigRollbackResponse
        {
            OriginalChangeRequestId = targetChange.ChangeRequestId,
            NewChangeRequestId = response.ChangeRequestId,
            RollbackId = rollbackRecord.RollbackId,
            Message = policy.RequiresApproval
                ? "Rollback change request submitted for approval."
                : "Rollback applied successfully."
        };
    }

    public async Task<IReadOnlyCollection<ConfigChangeHistoryDto>> GetChangeHistoryAsync(string configKey, int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var history = await _dbContext.ConfigurationChanges.AsNoTracking()
            .Where(c => c.ConfigKey == configKey)
            .OrderByDescending(c => c.RequestedAt)
            .Take(safeLimit)
            .Select(c => new ConfigChangeHistoryDto
            {
                ChangeRequestId = c.ChangeRequestId,
                Status = c.Status,
                OldValue = RedactValue(c.Sensitivity, c.OldValue),
                NewValue = RedactValue(c.Sensitivity, c.NewValue),
                RequestedAt = c.RequestedAt,
                AppliedAt = c.AppliedAt,
                RequestedBy = c.RequestedBy,
                ApprovedBy = c.ApprovedBy,
                GitCommitSha = c.GitCommitSha
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return history;
    }

    public async Task UpdatePolicyAsync(int policyId, ConfigPolicyUpdateDto update, string adminId, CancellationToken cancellationToken)
    {
        var policy = await _dbContext.ConfigurationPolicies
            .FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Configuration policy not found");

        if (!string.IsNullOrWhiteSpace(update.Category))
        {
            policy.Category = update.Category;
        }

        if (update.RequiresApproval.HasValue)
        {
            policy.RequiresApproval = update.RequiresApproval.Value;
        }

        if (!string.IsNullOrWhiteSpace(update.Sensitivity))
        {
            policy.Sensitivity = update.Sensitivity;
        }

        policy.AllowedValuesRegex = update.AllowedValuesRegex ?? policy.AllowedValuesRegex;
        policy.AllowedValuesList = update.AllowedValuesList ?? policy.AllowedValuesList;
        policy.Description = update.Description ?? policy.Description;
        policy.UpdatedBy = adminId;
        policy.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(new AuditEvent
        {
            Actor = adminId,
            Action = "ConfigPolicyUpdated",
            EntityType = "ConfigurationPolicy",
            EntityId = policy.Id.ToString(),
            EventData = JsonSerializer.Serialize(new
            {
                policy.ConfigKey,
                policy.Category,
                policy.RequiresApproval,
                policy.Sensitivity
            })
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyChangeAsync(ConfigurationChange change, ConfigurationPolicy policy, string actor, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _deployer.ApplyChangeAsync(change, policy, cancellationToken).ConfigureAwait(false);
            change.GitCommitSha = result.GitCommitSha;
            change.AppliedAt = result.AppliedAt;
            change.Status = "Applied";

            policy.CurrentValue = change.NewValue;
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = actor;

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await LogAuditAsync(new AuditEvent
            {
                Actor = actor,
                Action = "ConfigChangeApplied",
                EntityType = "ConfigurationChange",
                EntityId = change.ChangeRequestId.ToString(),
                CorrelationId = change.CorrelationId,
                EventData = JsonSerializer.Serialize(new
                {
                    change.ConfigKey,
                    gitCommit = result.GitCommitSha,
                    appliedAt = result.AppliedAt
                })
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            change.Status = "Failed";
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to apply configuration change {ChangeId}", change.ChangeRequestId);
            throw;
        }
    }

    private static void ValidateValue(ConfigurationPolicy policy, string newValue)
    {
        if (!string.IsNullOrWhiteSpace(policy.AllowedValuesRegex) && !Regex.IsMatch(newValue, policy.AllowedValuesRegex))
        {
            throw new ValidationException($"Value for '{policy.ConfigKey}' does not match allowed pattern");
        }

        if (!string.IsNullOrWhiteSpace(policy.AllowedValuesList))
        {
            try
            {
                var allowed = JsonSerializer.Deserialize<List<string>>(policy.AllowedValuesList);
                if (allowed != null && allowed.All(v => !string.Equals(v, newValue, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ValidationException($"Value for '{policy.ConfigKey}' is not within the allowed list");
                }
            }
            catch (JsonException)
            {
                // ignore malformed allowed list; treat as no restriction
            }
        }
    }

    private static string? RedactValue(ConfigurationPolicy policy, string? value) => RedactValue(policy.Sensitivity, value);

    private static string? RedactValue(string sensitivity, string? value)
    {
        if (value is null)
        {
            return null;
        }

        return sensitivity is "High" or "Critical" ? "***REDACTED***" : value;
    }

    private Task LogAuditAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        => _auditService.LogEventAsync(auditEvent, cancellationToken);
}
