using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface IUserService
{
    Task<UserResponse?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<UserResponse?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<UserResponse?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserResponse?> GetUserByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default);
    Task<bool> ValidateUserCredentialsAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> UpdateLastLoginAsync(string userId, DateTime lastLoginAt, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> UpdateLockoutAsync(string userId, DateTimeOffset? lockoutEnd, int accessFailedCount, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> AssignRoleAsync(string userId, string roleId, string assignedBy, CancellationToken cancellationToken = default);
    Task<bool> RemoveRoleAsync(string userId, string roleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserResponse>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<UserResponse>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? BranchId { get; set; }
    public string[] RoleIds { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; } = true;
}

public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? BranchId { get; set; }
    public bool? IsActive { get; set; }
    public bool? EmailConfirmed { get; set; }
    public bool? PhoneNumberConfirmed { get; set; }
    public bool? TwoFactorEnabled { get; set; }
}
