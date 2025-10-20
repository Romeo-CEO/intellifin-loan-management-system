using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;
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

public sealed class AccessElevationService : IAccessElevationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminDbContext _dbContext;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IAuditService _auditService;
    private readonly IElevationNotificationService _notificationService;
    private readonly ICamundaWorkflowService _camundaWorkflowService;
    private readonly IOptionsMonitor<ElevationOptions> _optionsMonitor;
    private readonly ILogger<AccessElevationService> _logger;

    public AccessElevationService(
        AdminDbContext dbContext,
        IKeycloakAdminService keycloakAdminService,
        IAuditService auditService,
        IElevationNotificationService notificationService,
        ICamundaWorkflowService camundaWorkflowService,
        IOptionsMonitor<ElevationOptions> optionsMonitor,
        ILogger<AccessElevationService> logger)
    {
        _dbContext = dbContext;
        _keycloakAdminService = keycloakAdminService;
        _auditService = auditService;
        _notificationService = notificationService;
        _camundaWorkflowService = camundaWorkflowService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<ElevationRequestResponse> RequestElevationAsync(string userId, string userName, ElevationRequestDto request, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        ValidateRequest(request, options);

        var availableRoles = await _keycloakAdminService.GetRolesAsync(cancellationToken);
        var invalidRoles = request.RequestedRoles
            .Where(role => availableRoles.All(r => !string.Equals(r.Name, role, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (invalidRoles.Count > 0)
        {
            throw new ValidationException($"Invalid roles requested: {string.Join(", ", invalidRoles)}");
        }

        var (managerId, managerName) = await ResolveManagerAsync(userId, cancellationToken)
            ?? throw new ValidationException("Manager information is required before requesting elevation. Contact administration.");

        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var elevation = new ElevationRequest
        {
            ElevationId = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            RequestedRoles = JsonSerializer.Serialize(request.RequestedRoles, SerializerOptions),
            Justification = request.Justification,
            RequestedDuration = request.Duration,
            ManagerId = managerId,
            ManagerName = managerName,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _dbContext.ElevationRequests.Add(elevation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var processInstanceId = await _camundaWorkflowService.StartElevationWorkflowAsync(elevation, request.RequestedRoles, cancellationToken);
        if (!string.IsNullOrWhiteSpace(processInstanceId))
        {
            elevation.CamundaProcessInstanceId = processInstanceId;
            _dbContext.ElevationRequests.Update(elevation);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _notificationService.NotifyManagerPendingAsync(elevation, request.RequestedRoles, cancellationToken);

        await RecordAuditEventAsync(CreateAuditEvent(userId, "ElevationRequested", elevation, new
        {
            requestedRoles = request.RequestedRoles,
            request.Duration,
            request.Justification,
            managerId,
            managerName
        }), cancellationToken);

        _logger.LogInformation("Elevation request {ElevationId} created for user {UserId}", elevation.ElevationId, userId);

        return new ElevationRequestResponse
        {
            ElevationId = elevation.ElevationId,
            Status = elevation.Status,
            Message = "Elevation request submitted successfully. Awaiting approval.",
            EstimatedApprovalTime = DateTime.UtcNow.AddHours(options.ApprovalTimeoutHours / 2.0)
        };
    }

    public async Task<ElevationStatusDto?> GetElevationStatusAsync(Guid elevationId, CancellationToken cancellationToken)
    {
        var elevation = await _dbContext.ElevationRequests.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ElevationId == elevationId, cancellationToken);
        if (elevation is null)
        {
            return null;
        }

        return ToStatusDto(elevation);
    }

    public async Task<PagedResult<ElevationSummaryDto>> ListElevationsAsync(string requesterId, string? filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.ElevationRequests.AsNoTracking().AsQueryable();
        if (string.Equals(filter, "my-requests", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.UserId == requesterId);
        }
        else if (string.Equals(filter, "pending-approvals", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.ManagerId == requesterId && e.Status == "Pending");
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.RequestedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var summaries = items.Select(ToSummaryDto).ToList();
        return new PagedResult<ElevationSummaryDto>(summaries, safePage, safePageSize, total);
    }

    public async Task ApproveElevationAsync(Guid elevationId, string managerId, string managerName, int approvedDuration, CancellationToken cancellationToken)
    {
        var elevation = await _dbContext.ElevationRequests.FirstOrDefaultAsync(e => e.ElevationId == elevationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Elevation request {elevationId} was not found.");

        if (!string.Equals(elevation.ManagerId, managerId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("You are not authorized to approve this elevation request.");
        }

        if (!string.Equals(elevation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Elevation request is not pending. Current status: {elevation.Status}");
        }

        var options = _optionsMonitor.CurrentValue;
        if (approvedDuration <= 0 || approvedDuration > Math.Min(options.MaxDurationMinutes, elevation.RequestedDuration))
        {
            throw new ValidationException("Approved duration exceeds allowed limits.");
        }

        var requestedRoles = DeserializeRoles(elevation.RequestedRoles);

        elevation.Status = "Approved";
        elevation.ApprovedBy = managerId;
        elevation.ApprovedDuration = approvedDuration;
        elevation.ApprovedAt = DateTime.UtcNow;
        elevation.ExpiresAt = DateTime.UtcNow.AddMinutes(approvedDuration);
        elevation.UpdatedAt = DateTime.UtcNow;
        _dbContext.ElevationRequests.Update(elevation);

        if (!string.IsNullOrWhiteSpace(elevation.CamundaProcessInstanceId))
        {
            await _camundaWorkflowService.CompleteManagerApprovalAsync(elevation.CamundaProcessInstanceId, approved: true, cancellationToken);
        }

        await _keycloakAdminService.AssignRolesAsync(elevation.UserId, new AssignRolesRequest { Roles = requestedRoles }, cancellationToken);
        await _keycloakAdminService.SetUserAttributeAsync(elevation.UserId, $"jit_elevation_{elevation.ElevationId}", JsonSerializer.Serialize(new
        {
            elevationId = elevation.ElevationId,
            approvedBy = managerId,
            expiresAt = elevation.ExpiresAt,
            roles = requestedRoles
        }, SerializerOptions), cancellationToken);
        await _keycloakAdminService.InvalidateUserSessionsAsync(elevation.UserId, cancellationToken);

        elevation.Status = "Active";
        elevation.ActivatedAt = DateTime.UtcNow;
        elevation.UpdatedAt = DateTime.UtcNow;
        _dbContext.ElevationRequests.Update(elevation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyRequesterApprovedAsync(elevation, requestedRoles, cancellationToken);

        await RecordAuditEventAsync(CreateAuditEvent(managerId, "ElevationApproved", elevation, new
        {
            approvedDuration,
            expiresAt = elevation.ExpiresAt,
            managerName
        }), cancellationToken);

        await RecordAuditEventAsync(CreateAuditEvent(elevation.UserId, "ElevationActivated", elevation, new
        {
            roles = requestedRoles,
            expiresAt = elevation.ExpiresAt
        }), cancellationToken);

        _logger.LogInformation("Elevation request {ElevationId} approved by {ManagerId}", elevation.ElevationId, managerId);
    }

    public async Task RejectElevationAsync(Guid elevationId, string managerId, string managerName, string reason, CancellationToken cancellationToken)
    {
        var elevation = await _dbContext.ElevationRequests.FirstOrDefaultAsync(e => e.ElevationId == elevationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Elevation request {elevationId} was not found.");

        if (!string.Equals(elevation.ManagerId, managerId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("You are not authorized to reject this elevation request.");
        }

        if (!string.Equals(elevation.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Elevation request is not pending. Current status: {elevation.Status}");
        }

        elevation.Status = "Rejected";
        elevation.RejectedBy = managerId;
        elevation.RejectionReason = reason;
        elevation.RejectedAt = DateTime.UtcNow;
        elevation.UpdatedAt = DateTime.UtcNow;
        _dbContext.ElevationRequests.Update(elevation);

        if (!string.IsNullOrWhiteSpace(elevation.CamundaProcessInstanceId))
        {
            await _camundaWorkflowService.CompleteManagerApprovalAsync(elevation.CamundaProcessInstanceId, approved: false, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyRequesterRejectedAsync(elevation, reason, cancellationToken);

        await RecordAuditEventAsync(CreateAuditEvent(managerId, "ElevationRejected", elevation, new
        {
            reason,
            managerName
        }), cancellationToken);

        _logger.LogInformation("Elevation request {ElevationId} rejected by {ManagerId}", elevation.ElevationId, managerId);
    }

    public async Task RevokeElevationAsync(Guid elevationId, string adminId, string adminName, string reason, CancellationToken cancellationToken)
    {
        var elevation = await _dbContext.ElevationRequests.FirstOrDefaultAsync(e => e.ElevationId == elevationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Elevation request {elevationId} was not found.");

        if (!string.Equals(elevation.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Elevation request is not active. Current status: {elevation.Status}");
        }

        var roles = DeserializeRoles(elevation.RequestedRoles);
        foreach (var role in roles)
        {
            await _keycloakAdminService.RemoveRoleAsync(elevation.UserId, role, cancellationToken);
        }

        await _keycloakAdminService.RemoveUserAttributeAsync(elevation.UserId, $"jit_elevation_{elevation.ElevationId}", cancellationToken);
        await _keycloakAdminService.InvalidateUserSessionsAsync(elevation.UserId, cancellationToken);

        elevation.Status = "Revoked";
        elevation.RevokedBy = adminId;
        elevation.RevocationReason = reason;
        elevation.RevokedAt = DateTime.UtcNow;
        elevation.UpdatedAt = DateTime.UtcNow;
        _dbContext.ElevationRequests.Update(elevation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.NotifyRequesterRevokedAsync(elevation, reason, cancellationToken);

        await RecordAuditEventAsync(CreateAuditEvent(adminId, "ElevationRevoked", elevation, new
        {
            reason,
            adminName,
            roles
        }), cancellationToken);

        _logger.LogWarning("Elevation request {ElevationId} revoked by {AdminId}", elevation.ElevationId, adminId);
    }

    public async Task<IReadOnlyCollection<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.ElevationRequests.AsNoTracking()
            .Where(e => e.Status == "Active" && e.ExpiresAt > DateTime.UtcNow)
            .OrderBy(e => e.ExpiresAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(e => new ActiveSessionDto
        {
            ElevationId = e.ElevationId,
            UserId = e.UserId,
            UserName = e.UserName,
            Roles = DeserializeRoles(e.RequestedRoles),
            ActivatedAt = e.ActivatedAt ?? DateTime.UtcNow,
            ExpiresAt = e.ExpiresAt ?? DateTime.UtcNow,
            ApprovedBy = e.ApprovedBy ?? string.Empty,
            ManagerName = e.ManagerName
        }).ToList();
    }

    public async Task<int> ExpireElevationsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await _dbContext.ElevationRequests
            .Where(e => e.Status == "Active" && e.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var elevation in expired)
        {
            var roles = DeserializeRoles(elevation.RequestedRoles);
            foreach (var role in roles)
            {
                await _keycloakAdminService.RemoveRoleAsync(elevation.UserId, role, cancellationToken);
            }

            await _keycloakAdminService.RemoveUserAttributeAsync(elevation.UserId, $"jit_elevation_{elevation.ElevationId}", cancellationToken);
            await _keycloakAdminService.InvalidateUserSessionsAsync(elevation.UserId, cancellationToken);

            elevation.Status = "Expired";
            elevation.ExpiredAt = now;
            elevation.UpdatedAt = now;
            _dbContext.ElevationRequests.Update(elevation);

            await RecordAuditEventAsync(CreateAuditEvent("SYSTEM", "ElevationExpired", elevation, new
            {
                elevation.UserId,
                roles,
                expiredAt = now
            }), cancellationToken);

            if (_optionsMonitor.CurrentValue.NotifyUserOnExpiration)
            {
                await _notificationService.NotifyRequesterExpiredAsync(elevation, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Processed {Count} expired elevations", expired.Count);
        return expired.Count;
    }

    private static void ValidateRequest(ElevationRequestDto request, ElevationOptions options)
    {
        if (request.Duration <= 0 || request.Duration > options.MaxDurationMinutes)
        {
            throw new ValidationException($"Duration must be between 1 and {options.MaxDurationMinutes} minutes");
        }

        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length < options.MinJustificationLength)
        {
            throw new ValidationException($"Justification must be at least {options.MinJustificationLength} characters");
        }
    }

    private async Task<(string Id, string Name)?> ResolveManagerAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _keycloakAdminService.GetUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!TryGetAttribute(user.Attributes, "managerId", out var managerId) || string.IsNullOrWhiteSpace(managerId))
        {
            return null;
        }

        var manager = await _keycloakAdminService.GetUserAsync(managerId, cancellationToken);
        if (manager is null)
        {
            return null;
        }

        var managerName = string.Join(' ', new[] { manager.FirstName, manager.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (string.IsNullOrWhiteSpace(managerName))
        {
            managerName = manager.Username;
        }

        return (manager.Id, managerName);
    }

    private static bool TryGetAttribute(IReadOnlyDictionary<string, IReadOnlyList<string>>? attributes, string key, out string? value)
    {
        value = null;
        if (attributes is null)
        {
            return false;
        }

        foreach (var pair in attributes)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && pair.Value.Count > 0)
            {
                value = pair.Value[0];
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyCollection<string> DeserializeRoles(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        try
        {
            var roles = JsonSerializer.Deserialize<List<string>>(value, SerializerOptions);
            return roles is null
                ? Array.Empty<string>()
                : roles.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static ElevationStatusDto ToStatusDto(ElevationRequest elevation)
    {
        return new ElevationStatusDto
        {
            ElevationId = elevation.ElevationId,
            Status = elevation.Status,
            UserId = elevation.UserId,
            UserName = elevation.UserName,
            RequestedRoles = DeserializeRoles(elevation.RequestedRoles),
            RequestedDuration = elevation.RequestedDuration,
            ApprovedDuration = elevation.ApprovedDuration,
            RequestedAt = elevation.RequestedAt,
            ApprovedAt = elevation.ApprovedAt,
            ActivatedAt = elevation.ActivatedAt,
            ExpiresAt = elevation.ExpiresAt,
            ManagerId = elevation.ManagerId,
            ManagerName = elevation.ManagerName,
            Justification = elevation.Justification
        };
    }

    private static ElevationSummaryDto ToSummaryDto(ElevationRequest elevation)
    {
        return new ElevationSummaryDto
        {
            ElevationId = elevation.ElevationId,
            UserId = elevation.UserId,
            UserName = elevation.UserName,
            Status = elevation.Status,
            RequestedRoles = DeserializeRoles(elevation.RequestedRoles),
            RequestedDuration = elevation.RequestedDuration,
            ApprovedDuration = elevation.ApprovedDuration,
            RequestedAt = elevation.RequestedAt,
            ExpiresAt = elevation.ExpiresAt,
            ManagerName = elevation.ManagerName
        };
    }

    private async Task RecordAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await _auditService.LogEventAsync(auditEvent, cancellationToken);
        await _auditService.FlushBufferAsync(cancellationToken);
    }

    private static AuditEvent CreateAuditEvent(string actor, string action, ElevationRequest elevation, object details)
    {
        return new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = actor,
            Action = action,
            EntityType = "ElevationRequest",
            EntityId = elevation.ElevationId.ToString(),
            CorrelationId = elevation.CorrelationId,
            EventData = JsonSerializer.Serialize(details, SerializerOptions)
        };
    }
}
