using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for managing tenants and tenant-user memberships
/// </summary>
public class TenantService : ITenantService
{
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IBackgroundQueue<ProvisionCommand>? _provisioningQueue;
    private readonly ILogger<TenantService> _logger;
    private readonly FeatureFlags _featureFlags;

    public TenantService(
        LmsDbContext dbContext,
        IAuditService auditService,
        ILogger<TenantService> logger,
        IOptions<FeatureFlags> featureFlags,
        IBackgroundQueue<ProvisionCommand>? provisioningQueue = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
        _featureFlags = featureFlags.Value;
        _provisioningQueue = provisioningQueue;
    }

    public async Task<TenantDto> CreateTenantAsync(TenantCreateRequest request, CancellationToken ct = default)
    {
        // Check for duplicate code
        var existingTenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Code == request.Code, ct);

        if (existingTenant != null)
        {
            _logger.LogWarning("Attempt to create tenant with duplicate code: {Code}", request.Code);
            throw new InvalidOperationException($"Tenant with code '{request.Code}' already exists.");
        }

        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = request.Name,
            Code = request.Code,
            Settings = request.Settings,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created tenant {TenantId} with code {Code}", tenant.TenantId, tenant.Code);

        // Audit event
        await _auditService.LogAsync(new AuditEvent
        {
            Action = "TenantCreated",
            Entity = "Tenant",
            EntityId = tenant.TenantId.ToString(),
            Details = new Dictionary<string, object>
            {
                { "TenantName", tenant.Name },
                { "TenantCode", tenant.Code }
            },
            ActorId = "system",
            Timestamp = DateTime.UtcNow
        }, ct);

        return MapToDto(tenant);
    }

    public async Task AssignUserToTenantAsync(Guid tenantId, string userId, string? role, CancellationToken ct = default)
    {
        // Validate tenant exists
        var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.TenantId == tenantId, ct);
        if (!tenantExists)
        {
            throw new InvalidOperationException($"Tenant with ID '{tenantId}' not found.");
        }

        // Upsert tenant user membership (idempotent)
        var existingMembership = await _dbContext.TenantUsers
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, ct);

        if (existingMembership != null)
        {
            // Update existing membership
            existingMembership.Role = role;
            existingMembership.AssignedAt = DateTime.UtcNow;
            existingMembership.AssignedBy = "system";

            _logger.LogInformation("Updated user {UserId} membership in tenant {TenantId} with role {Role}", userId, tenantId, role);
        }
        else
        {
            // Create new membership
            var tenantUser = new TenantUser
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = "system"
            };

            _dbContext.TenantUsers.Add(tenantUser);
            _logger.LogInformation("Assigned user {UserId} to tenant {TenantId} with role {Role}", userId, tenantId, role);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Audit event
        await _auditService.LogAsync(new AuditEvent
        {
            Action = "UserAssigned",
            Entity = "TenantUser",
            EntityId = $"{tenantId}|{userId}",
            Details = new Dictionary<string, object>
            {
                { "TenantId", tenantId },
                { "UserId", userId },
                { "Role", role ?? "none" }
            },
            ActorId = "system",
            Timestamp = DateTime.UtcNow
        }, ct);

        // Trigger provisioning if enabled
        if (_featureFlags.EnableUserProvisioning && _provisioningQueue != null)
        {
            await _provisioningQueue.QueueAsync(new ProvisionCommand
            {
                UserId = userId,
                Reason = "MembershipChanged",
                CorrelationId = Guid.NewGuid(),
                QueuedAt = DateTime.UtcNow
            }, ct);

            _logger.LogDebug("Queued provisioning command for user {UserId} after membership change", userId);
        }
    }

    public async Task RemoveUserFromTenantAsync(Guid tenantId, string userId, CancellationToken ct = default)
    {
        var membership = await _dbContext.TenantUsers
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, ct);

        if (membership == null)
        {
            _logger.LogWarning("Attempt to remove non-existent membership: User {UserId} from Tenant {TenantId}", userId, tenantId);
            return; // Idempotent - no error if already removed
        }

        _dbContext.TenantUsers.Remove(membership);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Removed user {UserId} from tenant {TenantId}", userId, tenantId);

        // Audit event
        await _auditService.LogAsync(new AuditEvent
        {
            Action = "UserRemoved",
            Entity = "TenantUser",
            EntityId = $"{tenantId}|{userId}",
            Details = new Dictionary<string, object>
            {
                { "TenantId", tenantId },
                { "UserId", userId }
            },
            ActorId = "system",
            Timestamp = DateTime.UtcNow
        }, ct);

        // Trigger provisioning if enabled
        if (_featureFlags.EnableUserProvisioning && _provisioningQueue != null)
        {
            await _provisioningQueue.QueueAsync(new ProvisionCommand
            {
                UserId = userId,
                Reason = "MembershipChanged",
                CorrelationId = Guid.NewGuid(),
                QueuedAt = DateTime.UtcNow
            }, ct);

            _logger.LogDebug("Queued provisioning command for user {UserId} after membership removal", userId);
        }
    }

    public async Task<PagedResult<TenantDto>> ListTenantsAsync(int page, int pageSize, bool? isActive, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100; // Max page size

        var query = _dbContext.Tenants.AsQueryable();

        // Apply filter if provided
        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(ct);

        // Apply paging
        var tenants = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TenantDto>
        {
            Items = tenants.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private static TenantDto MapToDto(Tenant tenant)
    {
        return new TenantDto
        {
            TenantId = tenant.TenantId,
            Name = tenant.Name,
            Code = tenant.Code,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            Settings = tenant.Settings
        };
    }
}
