using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Models.Keycloak;

namespace IntelliFin.AdminService.Services;

public interface IKeycloakAdminService
{
    Task<PagedResult<UserResponse>> GetUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<UserResponse?> GetUserAsync(string id, CancellationToken cancellationToken);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task DeleteUserAsync(string id, CancellationToken cancellationToken);
    Task ResetUserPasswordAsync(string id, ResetPasswordRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken);
    Task<RoleResponse?> GetRoleAsync(string name, CancellationToken cancellationToken);
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);
    Task<RoleResponse> UpdateRoleAsync(string name, UpdateRoleRequest request, CancellationToken cancellationToken);
    Task DeleteRoleAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoleResponse>> GetUserRolesAsync(string id, CancellationToken cancellationToken);
    Task AssignRolesAsync(string id, AssignRolesRequest request, CancellationToken cancellationToken);
    Task RemoveRoleAsync(string id, string roleName, CancellationToken cancellationToken);
    Task SetUserAttributeAsync(string id, string attributeName, string value, CancellationToken cancellationToken);
    Task RemoveUserAttributeAsync(string id, string attributeName, CancellationToken cancellationToken);
    Task InvalidateUserSessionsAsync(string id, CancellationToken cancellationToken);
}
