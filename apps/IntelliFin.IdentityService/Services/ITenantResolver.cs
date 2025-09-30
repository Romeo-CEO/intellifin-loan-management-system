namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for resolving tenant context from HTTP requests
/// Supports the two-plane architecture (Platform vs Tenant planes)
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Gets the current tenant ID from the request context
    /// Returns null for Platform Plane operations (IntelliFin internal team)
    /// Returns Guid for Tenant Plane operations (customer organizations)
    /// </summary>
    string? GetCurrentTenantId();

    /// <summary>
    /// Determines if the current request is operating in Platform Plane
    /// (IntelliFin internal team with TenantId = null + PlatformAdmin role)
    /// </summary>
    bool IsPlatformPlane();

    /// <summary>
    /// Determines if the current request is operating in Tenant Plane
    /// (Customer organizations with TenantId = Guid + tenant-specific roles)
    /// </summary>
    bool IsTenantPlane();

    /// <summary>
    /// Gets the current user ID from the request context
    /// </summary>
    string? GetCurrentUserId();

    /// <summary>
    /// Gets the current user's roles from the JWT token
    /// </summary>
    string[] GetCurrentUserRoles();

    /// <summary>
    /// Gets the current user's permissions from the JWT token
    /// </summary>
    string[] GetCurrentUserPermissions();

    /// <summary>
    /// Validates that the current user has access to the specified tenant
    /// Platform Plane users have access to all tenants
    /// Tenant Plane users only have access to their own tenant
    /// </summary>
    Task<bool> ValidateTenantAccessAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tenant information for the current context
    /// Returns null for Platform Plane operations
    /// </summary>
    Task<TenantInfo?> GetCurrentTenantInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the tenant ID from the provided user claims
    /// Returns null for Platform Plane operations
    /// </summary>
    Task<Guid?> GetTenantIdAsync(System.Security.Claims.ClaimsPrincipal user);
}

/// <summary>
/// Basic tenant information for context resolution
/// </summary>
public class TenantInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string[] EnabledFeatures { get; set; } = Array.Empty<string>();
}