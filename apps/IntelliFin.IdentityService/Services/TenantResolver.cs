using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Services;

public class TenantResolver : ITenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentTenantId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("tenant_id")?.Value;
    }

    public bool IsPlatformPlane()
    {
        return GetCurrentTenantId() is null;
    }

    public bool IsTenantPlane()
    {
        return GetCurrentTenantId() is not null;
    }

    public string? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? user?.FindFirst("sub")?.Value
               ?? user?.FindFirst("user_id")?.Value;
    }

    public string[] GetCurrentUserRoles()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
    }

    public string[] GetCurrentUserPermissions()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindAll("permission").Select(c => c.Value).ToArray() ?? Array.Empty<string>();
    }

    public Task<bool> ValidateTenantAccessAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (IsPlatformPlane()) return Task.FromResult(true);
        var current = GetCurrentTenantId();
        return Task.FromResult(string.Equals(current, tenantId, StringComparison.OrdinalIgnoreCase));
    }

    public Task<TenantInfo?> GetCurrentTenantInfoAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId is null) return Task.FromResult<TenantInfo?>(null);

        // Basic info from claims only; extend to fetch from DB if needed
        var info = new TenantInfo
        {
            Id = tenantId,
            Name = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_name")?.Value ?? string.Empty,
            IsActive = true,
            EnabledFeatures = Array.Empty<string>(),
            SubscriptionTier = "standard"
        };
        return Task.FromResult<TenantInfo?>(info);
    }

    public Task<Guid?> GetTenantIdAsync(ClaimsPrincipal user)
    {
        var value = user.FindFirst("tenant_id")?.Value;
        return Task.FromResult(Guid.TryParse(value, out var id) ? id : (Guid?)null);
    }
}
