using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;

namespace IntelliFin.IdentityService.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<UserResponse?> GetUserByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<UserResponse?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<UserResponse?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<UserResponse?> GetUserByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameOrEmailAsync(usernameOrEmail, cancellationToken);
        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<bool> ValidateUserCredentialsAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByUsernameOrEmailAsync(usernameOrEmail, cancellationToken);
            
            if (user == null || !user.CanLogin)
            {
                _logger.LogWarning("User validation failed: User not found or cannot login for {UsernameOrEmail}", usernameOrEmail);
                return false;
            }

            var isValidPassword = await _passwordService.VerifyPasswordAsync(password, user.PasswordHash, cancellationToken);
            
            if (!isValidPassword)
            {
                _logger.LogWarning("User validation failed: Invalid password for {UsernameOrEmail}", usernameOrEmail);
                return false;
            }

            _logger.LogInformation("User validation successful for {UsernameOrEmail}", usernameOrEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating user credentials for {UsernameOrEmail}", usernameOrEmail);
            return false;
        }
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Validate unique constraints
        if (await _userRepository.UsernameExistsAsync(request.Username, cancellationToken))
        {
            throw new InvalidOperationException($"Username '{request.Username}' already exists");
        }

        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new InvalidOperationException($"Email '{request.Email}' already exists");
        }

        // Hash password
        var passwordHash = await _passwordService.HashPasswordAsync(request.Password, cancellationToken);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = passwordHash,
            BranchId = request.BranchId,
            IsActive = request.IsActive,
            CreatedBy = "system", // This would come from the current user context
            CreatedAt = DateTime.UtcNow
        };

        var createdUser = await _userRepository.CreateAsync(user, cancellationToken);
        
        _logger.LogInformation("User created successfully: {UserId} - {Username}", createdUser.Id, createdUser.Username);
        
        return MapToUserResponse(createdUser);
    }

    public async Task<UserResponse> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID '{id}' not found");
        }

        // Update fields if provided
        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
            {
                throw new InvalidOperationException($"Email '{request.Email}' already exists");
            }
            user.Email = request.Email;
        }

        if (!string.IsNullOrEmpty(request.FirstName)) user.FirstName = request.FirstName;
        if (!string.IsNullOrEmpty(request.LastName)) user.LastName = request.LastName;
        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
        if (request.BranchId != null) user.BranchId = request.BranchId;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        if (request.EmailConfirmed.HasValue) user.EmailConfirmed = request.EmailConfirmed.Value;
        if (request.PhoneNumberConfirmed.HasValue) user.PhoneNumberConfirmed = request.PhoneNumberConfirmed.Value;
        if (request.TwoFactorEnabled.HasValue) user.TwoFactorEnabled = request.TwoFactorEnabled.Value;

        user.UpdatedBy = "system"; // This would come from the current user context
        user.UpdatedAt = DateTime.UtcNow;

        var updatedUser = await _userRepository.UpdateAsync(user, cancellationToken);
        
        _logger.LogInformation("User updated successfully: {UserId} - {Username}", updatedUser.Id, updatedUser.Username);
        
        return MapToUserResponse(updatedUser);
    }

    public async Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await _userRepository.DeleteAsync(id, cancellationToken);
        if (result)
        {
            _logger.LogInformation("User deleted successfully: {UserId}", id);
        }
        return result;
    }

    public async Task<bool> UpdateLastLoginAsync(string userId, DateTime lastLoginAt, CancellationToken cancellationToken = default)
    {
        return await _userRepository.UpdateLastLoginAsync(userId, lastLoginAt, cancellationToken);
    }

    public async Task<bool> UpdatePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return false;

        // Verify current password
        var isCurrentPasswordValid = await _passwordService.VerifyPasswordAsync(currentPassword, user.PasswordHash, cancellationToken);
        if (!isCurrentPasswordValid) return false;

        // Hash new password
        var newPasswordHash = await _passwordService.HashPasswordAsync(newPassword, cancellationToken);
        
        return await _userRepository.UpdatePasswordHashAsync(userId, newPasswordHash, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var newPasswordHash = await _passwordService.HashPasswordAsync(newPassword, cancellationToken);
        return await _userRepository.UpdatePasswordHashAsync(userId, newPasswordHash, cancellationToken);
    }

    public async Task<bool> UpdateLockoutAsync(string userId, DateTimeOffset? lockoutEnd, int accessFailedCount, CancellationToken cancellationToken = default)
    {
        return await _userRepository.UpdateLockoutAsync(userId, lockoutEnd, accessFailedCount, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetUserRolesAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetUserPermissionsAsync(userId, cancellationToken);
    }

    public async Task<bool> AssignRoleAsync(string userId, string roleId, string assignedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userRepository.AssignRoleAsync(userId, roleId, assignedBy, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
            return false;
        }
    }

    public async Task<bool> RemoveRoleAsync(string userId, string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userRepository.RemoveRoleAsync(userId, roleId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
            return false;
        }
    }

    public async Task<IEnumerable<UserResponse>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(MapToUserResponse);
    }

    public async Task<IEnumerable<UserResponse>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetActiveUsersAsync(cancellationToken);
        return users.Select(MapToUserResponse);
    }

    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            BranchId = user.BranchId,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Roles = user.UserRoles?.Where(ur => ur.IsActive).Select(ur => ur.Role.Name).ToArray() ?? Array.Empty<string>()
        };
    }
}
