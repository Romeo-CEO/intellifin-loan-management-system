using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Shared.DomainModels.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetUsersByRoleAsync(string roleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateLastLoginAsync(string userId, DateTime lastLoginAt, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordHashAsync(string userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<bool> UpdateLockoutAsync(string userId, DateTimeOffset? lockoutEnd, int accessFailedCount, CancellationToken cancellationToken = default);
    Task<bool> AssignRoleAsync(string userId, string roleId, string assignedBy, CancellationToken cancellationToken = default);
    Task<bool> RemoveRoleAsync(string userId, string roleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TenantUser>> GetUserTenantsAsync(string userId, CancellationToken cancellationToken = default);
}
