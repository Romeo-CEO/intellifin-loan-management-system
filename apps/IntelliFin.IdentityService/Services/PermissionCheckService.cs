using System.Collections.Generic;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.IdentityService.Services;

public class PermissionCheckService : IPermissionCheckService
{
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<PermissionCheckService> _logger;

    public PermissionCheckService(LmsDbContext dbContext, IAuditService auditService, ILogger<PermissionCheckService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PermissionCheckResponse> CheckPermissionAsync(PermissionCheckRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Permission))
        {
            return await DenyAsync(request, "invalid_request", cancellationToken).ConfigureAwait(false);
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles.Where(ur => ur.IsActive))
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return await DenyAsync(request, "user_not_found", cancellationToken).ConfigureAwait(false);
        }

        var branchMatch = EvaluateBranchContext(user, request.Context?.BranchId);
        if (!branchMatch)
        {
            return await DenyAsync(request, "branch_mismatch", cancellationToken, user.Id).ConfigureAwait(false);
        }

        var tenantMatch = EvaluateTenantContext(user, request.Context?.TenantId);
        if (!tenantMatch)
        {
            return await DenyAsync(request, "tenant_mismatch", cancellationToken, user.Id).ConfigureAwait(false);
        }

        var grantedPermissions = user.UserRoles
            .Where(ur => ur.IsActive && ur.Role is not null && ur.Role.IsActive)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.IsActive && rp.Permission is not null && rp.Permission.IsActive)
            .Select(rp => rp.Permission!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allowed = grantedPermissions.Contains(request.Permission, StringComparer.OrdinalIgnoreCase);
        if (!allowed)
        {
            return await DenyAsync(request, "permission_not_granted", cancellationToken, user.Id).ConfigureAwait(false);
        }

        await LogDecisionAsync(request, true, "granted", cancellationToken, user.Id).ConfigureAwait(false);
        _logger.LogInformation("Permission {Permission} granted for user {UserId}", request.Permission, request.UserId);

        return new PermissionCheckResponse
        {
            Allowed = true,
            Reason = "granted"
        };
    }

    private bool EvaluateBranchContext(User user, Guid? branchId)
    {
        if (branchId is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(user.BranchId))
        {
            return false;
        }

        return Guid.TryParse(user.BranchId, out var userBranch) && userBranch == branchId.Value;
    }

    private bool EvaluateTenantContext(User user, Guid? tenantId)
    {
        if (tenantId is null)
        {
            return true;
        }

        if (user.Metadata is null || user.Metadata.Count == 0)
        {
            return false;
        }

        if (user.Metadata.TryGetValue("tenantId", out var tenantValue) || user.Metadata.TryGetValue("tenant_id", out tenantValue))
        {
            if (tenantValue is not null && Guid.TryParse(tenantValue.ToString(), out var userTenant))
            {
                return userTenant == tenantId.Value;
            }
        }

        return false;
    }

    private async Task<PermissionCheckResponse> DenyAsync(PermissionCheckRequest request, string reason, CancellationToken cancellationToken, string? userId = null)
    {
        await LogDecisionAsync(request, false, reason, cancellationToken, userId).ConfigureAwait(false);
        _logger.LogWarning("Permission {Permission} denied for user {UserId} due to {Reason}", request.Permission, request.UserId, reason);

        return new PermissionCheckResponse
        {
            Allowed = false,
            Reason = reason
        };
    }

    private async Task LogDecisionAsync(PermissionCheckRequest request, bool allowed, string reason, CancellationToken cancellationToken, string? userId)
    {
        var details = new Dictionary<string, object>
        {
            ["permission"] = request.Permission,
            ["allowed"] = allowed,
            ["reason"] = reason
        };

        if (request.Context?.BranchId is Guid branchId)
        {
            details["branchId"] = branchId;
        }

        if (request.Context?.TenantId is Guid tenantId)
        {
            details["tenantId"] = tenantId;
        }

        var auditEvent = new AuditEvent
        {
            ActorId = request.UserId,
            Action = "permission.check",
            Entity = "user",
            EntityId = userId ?? request.UserId,
            Timestamp = DateTime.UtcNow,
            Success = allowed,
            Result = reason,
            Details = details
        };

        await _auditService.LogAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }
}
