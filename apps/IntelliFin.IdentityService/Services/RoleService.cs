using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;

namespace IntelliFin.IdentityService.Services;

public class RoleService : IRoleService
{
    private readonly ILogger<RoleService> _logger;
    
    // In-memory storage for demo purposes - would use EF Core DbContext in production
    private static readonly List<Role> _roles = new()
    {
        new Role
        {
            Id = "1",
            Name = "CEO",
            Description = "Chief Executive Officer with full system access",
            Type = RoleType.Organizational,
            IsActive = true,
            IsSystemRole = true,
            Level = 0,
            CreatedBy = "system"
        },
        new Role
        {
            Id = "2",
            Name = "Manager",
            Description = "Branch Manager with management privileges",
            Type = RoleType.Organizational,
            IsActive = true,
            IsSystemRole = true,
            Level = 1,
            ParentRoleId = "1",
            CreatedBy = "system"
        },
        new Role
        {
            Id = "3",
            Name = "LoanOfficer",
            Description = "Loan Officer with customer service access",
            Type = RoleType.Functional,
            IsActive = true,
            IsSystemRole = true,
            Level = 2,
            ParentRoleId = "2",
            CreatedBy = "system"
        },
        new Role
        {
            Id = "4",
            Name = "ComplianceOfficer",
            Description = "Compliance Officer with audit and reporting access",
            Type = RoleType.Functional,
            IsActive = true,
            IsSystemRole = true,
            Level = 2,
            ParentRoleId = "2",
            CreatedBy = "system"
        },
        new Role
        {
            Id = "5",
            Name = "Admin",
            Description = "System Administrator with technical privileges",
            Type = RoleType.System,
            IsActive = true,
            IsSystemRole = true,
            Level = 1,
            CreatedBy = "system"
        }
    };

    private static readonly Dictionary<string, List<string>> _userRoles = new();

    public RoleService(ILogger<RoleService> logger)
    {
        _logger = logger;
    }

    public Task<Role?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Id == roleId);
            return Task.FromResult(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role by ID {RoleId}", roleId);
            return Task.FromResult<Role?>(null);
        }
    }

    public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role by name {RoleName}", roleName);
            return Task.FromResult<Role?>(null);
        }
    }

    public Task<IEnumerable<Role>> GetAllRolesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = includeInactive 
                ? _roles.AsEnumerable()
                : _roles.Where(r => r.IsActive);

            return Task.FromResult<IEnumerable<Role>>(roles.OrderBy(r => r.Level).ThenBy(r => r.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all roles");
            return Task.FromResult(Enumerable.Empty<Role>());
        }
    }

    public Task<IEnumerable<Role>> GetRoleHierarchyAsync(string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null)
                return Task.FromResult(Enumerable.Empty<Role>());

            var hierarchy = new List<Role>();
            
            // Get parent roles
            var current = role;
            while (current != null)
            {
                hierarchy.Insert(0, current);
                current = !string.IsNullOrEmpty(current.ParentRoleId) 
                    ? _roles.FirstOrDefault(r => r.Id == current.ParentRoleId)
                    : null;
            }

            // Get child roles
            AddChildRoles(role, hierarchy);

            return Task.FromResult<IEnumerable<Role>>(hierarchy.Distinct());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role hierarchy for {RoleId}", roleId);
            return Task.FromResult(Enumerable.Empty<Role>());
        }
    }

    public Task<Role> CreateRoleAsync(RoleRequest request, string createdBy, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if role name already exists
            if (_roles.Any(r => r.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Role with name '{request.Name}' already exists");
            }

            var role = new Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                IsActive = request.IsActive,
                ParentRoleId = request.ParentRoleId,
                Level = CalculateRoleLevel(request.ParentRoleId),
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _roles.Add(role);

            _logger.LogInformation("Role created: {RoleId} - {RoleName} by {CreatedBy}", 
                role.Id, role.Name, createdBy);

            return Task.FromResult(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", request.Name);
            throw;
        }
    }

    public Task<Role> UpdateRoleAsync(string roleId, RoleRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null)
                throw new KeyNotFoundException($"Role with ID {roleId} not found");

            // Check if new name conflicts with existing roles (excluding current role)
            if (_roles.Any(r => r.Id != roleId && r.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Role with name '{request.Name}' already exists");
            }

            role.Name = request.Name;
            role.Description = request.Description;
            role.Type = request.Type;
            role.IsActive = request.IsActive;
            role.ParentRoleId = request.ParentRoleId;
            role.Level = CalculateRoleLevel(request.ParentRoleId);
            role.UpdatedBy = updatedBy;
            role.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Role updated: {RoleId} - {RoleName} by {UpdatedBy}", 
                role.Id, role.Name, updatedBy);

            return Task.FromResult(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", roleId);
            throw;
        }
    }

    public Task<bool> DeleteRoleAsync(string roleId, string deletedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null)
                return Task.FromResult(false);

            // Check if role is system role
            if (role.IsSystemRole)
            {
                throw new InvalidOperationException("Cannot delete system roles");
            }

            // Check if role has child roles
            if (_roles.Any(r => r.ParentRoleId == roleId))
            {
                throw new InvalidOperationException("Cannot delete role that has child roles");
            }

            // Check if role is assigned to users (in production this would check database)
            if (_userRoles.Values.Any(userRoleList => userRoleList.Contains(roleId)))
            {
                throw new InvalidOperationException("Cannot delete role that is assigned to users");
            }

            _roles.Remove(role);

            _logger.LogInformation("Role deleted: {RoleId} - {RoleName} by {DeletedBy}", 
                role.Id, role.Name, deletedBy);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", roleId);
            throw;
        }
    }

    public Task<bool> ActivateRoleAsync(string roleId, string activatedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null)
                return Task.FromResult(false);

            role.IsActive = true;
            role.UpdatedBy = activatedBy;
            role.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Role activated: {RoleId} - {RoleName} by {ActivatedBy}", 
                role.Id, role.Name, activatedBy);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating role {RoleId}", roleId);
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeactivateRoleAsync(string roleId, string deactivatedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = _roles.FirstOrDefault(r => r.Id == roleId);
            if (role == null)
                return Task.FromResult(false);

            // Check if role is system role
            if (role.IsSystemRole)
            {
                throw new InvalidOperationException("Cannot deactivate system roles");
            }

            role.IsActive = false;
            role.UpdatedBy = deactivatedBy;
            role.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Role deactivated: {RoleId} - {RoleName} by {DeactivatedBy}", 
                role.Id, role.Name, deactivatedBy);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating role {RoleId}", roleId);
            return Task.FromResult(false);
        }
    }

    public Task<IEnumerable<Role>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_userRoles.TryGetValue(userId, out var userRoleIds))
            {
                return Task.FromResult(Enumerable.Empty<Role>());
            }

            var roles = _roles.Where(r => userRoleIds.Contains(r.Id) && r.IsActive);
            return Task.FromResult(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles for user {UserId}", userId);
            return Task.FromResult(Enumerable.Empty<Role>());
        }
    }

    public Task<bool> HasRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_userRoles.TryGetValue(userId, out var userRoleIds))
            {
                return Task.FromResult(false);
            }

            var hasRole = _roles.Any(r => userRoleIds.Contains(r.Id) && 
                r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase) && r.IsActive);

            return Task.FromResult(hasRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} has role {RoleName}", userId, roleName);
            return Task.FromResult(false);
        }
    }

    private void AddChildRoles(Role parentRole, List<Role> hierarchy)
    {
        var childRoles = _roles.Where(r => r.ParentRoleId == parentRole.Id);
        foreach (var child in childRoles)
        {
            if (!hierarchy.Contains(child))
            {
                hierarchy.Add(child);
                AddChildRoles(child, hierarchy);
            }
        }
    }

    private int CalculateRoleLevel(string? parentRoleId)
    {
        if (string.IsNullOrEmpty(parentRoleId))
            return 0;

        var parentRole = _roles.FirstOrDefault(r => r.Id == parentRoleId);
        return parentRole?.Level + 1 ?? 0;
    }
}