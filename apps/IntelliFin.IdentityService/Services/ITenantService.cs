using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing tenants and tenant-user memberships
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Creates a new tenant with unique code
    /// </summary>
    /// <param name="request">Tenant creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created tenant DTO</returns>
    /// <exception cref="InvalidOperationException">Thrown when tenant code already exists</exception>
    Task<TenantDto> CreateTenantAsync(TenantCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Assigns a user to a tenant with optional role (idempotent)
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="role">Optional role for the user in this tenant</param>
    /// <param name="ct">Cancellation token</param>
    Task AssignUserToTenantAsync(Guid tenantId, string userId, string? role, CancellationToken ct = default);

    /// <summary>
    /// Removes a user from a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveUserFromTenantAsync(Guid tenantId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Lists tenants with pagination and optional filtering
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="isActive">Optional filter by active status</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paged list of tenants</returns>
    Task<PagedResult<TenantDto>> ListTenantsAsync(int page, int pageSize, bool? isActive, CancellationToken ct = default);
}
