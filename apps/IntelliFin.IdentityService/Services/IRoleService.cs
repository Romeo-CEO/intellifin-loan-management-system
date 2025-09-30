using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.IdentityService.Services;

public interface IRoleService
{
    Task<Role?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default);
    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<IEnumerable<Role>> GetAllRolesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Role>> GetRoleHierarchyAsync(string roleId, CancellationToken cancellationToken = default);
    Task<Role> CreateRoleAsync(RoleRequest request, string createdBy, CancellationToken cancellationToken = default);
    Task<Role> UpdateRoleAsync(string roleId, RoleRequest request, string updatedBy, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoleAsync(string roleId, string deletedBy, CancellationToken cancellationToken = default);
    Task<bool> ActivateRoleAsync(string roleId, string activatedBy, CancellationToken cancellationToken = default);
    Task<bool> DeactivateRoleAsync(string roleId, string deactivatedBy, CancellationToken cancellationToken = default);
    Task<IEnumerable<Role>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    // Additional methods for rule management
    Task<Role?> GetRoleAsync(string roleId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoleRule>> GetRoleRulesAsync(string roleId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<RoleRule> AddRuleToRoleAsync(string roleId, RoleRule rule, Guid tenantId, CancellationToken cancellationToken = default);
    Task<RoleRule> UpdateRoleRuleAsync(string roleId, string ruleId, RoleRule rule, Guid tenantId, CancellationToken cancellationToken = default);
    Task<RoleRule?> GetRoleRuleAsync(string roleId, string ruleId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> RemoveRuleFromRoleAsync(string roleId, string ruleId, string removedBy, Guid tenantId, CancellationToken cancellationToken = default);
}