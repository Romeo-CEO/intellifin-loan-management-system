using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using IntelliFin.ApiGateway.Security;
using IntelliFin.Shared.DomainModels.Services;

namespace IntelliFin.ApiGateway.Middleware;

public class BranchClaimMiddleware
{
    private static readonly string[] BranchIdClaimTypes = ["branch_id", "branchId"];
    private static readonly string[] BranchNameClaimTypes = ["branch_name", "branchName"];
    private static readonly string[] BranchRegionClaimTypes = ["branch_region", "branchRegion"];
    private const string BranchSwitchPermission = "branch:switch_context";

    private readonly RequestDelegate _next;
    private readonly ILogger<BranchClaimMiddleware> _logger;

    public BranchClaimMiddleware(RequestDelegate next, ILogger<BranchClaimMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var branchIdValue = GetFirstClaimValue(context.User, BranchIdClaimTypes);
            var branchNameValue = GetFirstClaimValue(context.User, BranchNameClaimTypes);
            var branchRegionValue = GetFirstClaimValue(context.User, BranchRegionClaimTypes);

            if (!string.IsNullOrWhiteSpace(branchIdValue))
            {
                context.Items[BranchClaimItemKeys.BranchIdRaw] = branchIdValue;

                if (int.TryParse(branchIdValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBranchId))
                {
                    context.Items[BranchClaimItemKeys.BranchId] = parsedBranchId;
                }
                else
                {
                    _logger.LogWarning("Unable to parse branch claim value '{BranchId}' to integer for user {User}", branchIdValue, context.User.Identity?.Name);
                }
            }

            if (!string.IsNullOrWhiteSpace(branchNameValue))
            {
                context.Items[BranchClaimItemKeys.BranchName] = branchNameValue;
            }

            if (!string.IsNullOrWhiteSpace(branchRegionValue))
            {
                context.Items[BranchClaimItemKeys.BranchRegion] = branchRegionValue;
            }

            await LogPotentialBranchOverrideAsync(context, branchIdValue, branchNameValue, branchRegionValue);
        }

        await _next(context);
    }

    private static string? GetFirstClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private async Task LogPotentialBranchOverrideAsync(HttpContext context, string? branchIdClaimValue, string? branchName, string? branchRegion)
    {
        if (string.IsNullOrWhiteSpace(branchIdClaimValue))
        {
            return;
        }

        var requestedBranchId = context.Request.Query["branchId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestedBranchId) || string.Equals(requestedBranchId, branchIdClaimValue, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.User.Claims.Any(c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Value, BranchSwitchPermission, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var auditService = context.RequestServices.GetService<IAuditService>();
        if (auditService is null)
        {
            _logger.LogWarning("Audit service not available to record branch scope override for user {User}", context.User.Identity?.Name ?? context.User.FindFirst("sub")?.Value ?? "unknown");
            return;
        }

        var actor = context.User.Identity?.Name
                     ?? context.User.FindFirst("preferred_username")?.Value
                     ?? context.User.FindFirst("sub")?.Value
                     ?? "unknown";

        var sessionId = context.User.FindFirst("session_id")?.Value;

        var data = new Dictionary<string, object>
        {
            ["requestedBranchId"] = requestedBranchId,
            ["userBranchId"] = branchIdClaimValue,
            ["userBranchName"] = branchName ?? string.Empty,
            ["userBranchRegion"] = branchRegion ?? string.Empty,
            ["path"] = context.Request.Path.ToString(),
            ["queryString"] = context.Request.QueryString.HasValue ? context.Request.QueryString.Value ?? string.Empty : string.Empty
        };

        try
        {
            await auditService.LogEventAsync(new AuditEventContext
            {
                Actor = actor,
                Action = "BRANCH_SCOPE_OVERRIDE_ATTEMPT",
                EntityType = "BranchAccess",
                EntityId = requestedBranchId ?? string.Empty,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                SessionId = sessionId,
                Source = "IntelliFin.ApiGateway",
                Category = AuditEventCategory.Authorization,
                Severity = AuditEventSeverity.Warning,
                Data = data,
                Success = false,
                OccurredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log branch scope override attempt for user {User}", actor);
        }
    }
}
