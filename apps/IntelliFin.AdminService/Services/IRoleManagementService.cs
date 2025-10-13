using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface IRoleManagementService
{
    Task<IReadOnlyCollection<RoleDefinitionDto>> GetAllRolesAsync(CancellationToken cancellationToken);
    Task<UserRolesDto?> GetUserRolesAsync(string userId, CancellationToken cancellationToken);
    Task<RoleAssignmentResult> AssignRoleAsync(
        string userId,
        string roleName,
        bool confirmedSodOverride,
        string adminId,
        string adminName,
        CancellationToken cancellationToken);
    Task RemoveRoleAsync(
        string userId,
        string roleName,
        string adminId,
        string adminName,
        string? reason,
        CancellationToken cancellationToken);
    Task<SodValidationResponse> ValidateSodAsync(
        string userId,
        string proposedRole,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoleHierarchyDto>> GetRoleHierarchyAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SodPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken);
}
