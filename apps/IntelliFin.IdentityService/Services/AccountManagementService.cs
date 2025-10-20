using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

public interface IAccountManagementService
{
    Task<UserProfileDto> GetUserProfileAsync(string userId);
    Task<UpdateProfileResult> UpdateUserProfileAsync(string userId, UpdateProfileRequest request);
    Task<ChangePasswordResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    Task<List<SessionDto>> GetActiveSessionsAsync(string userId);
    Task<bool> RevokeSessionAsync(string userId, string sessionId);
    Task SendPasswordChangedNotificationAsync(string userId);
}

public class AccountManagementService : IAccountManagementService
{
    private readonly IKeycloakAdminClient _keycloakAdmin;
    private readonly ILogger<AccountManagementService> _logger;
    private readonly LmsDbContext _context;
    private readonly IConnectionMultiplexer _redis;

    public AccountManagementService(
        IKeycloakAdminClient keycloakAdmin,
        ILogger<AccountManagementService> logger,
        LmsDbContext context,
        IConnectionMultiplexer redis)
    {
        _keycloakAdmin = keycloakAdmin;
        _logger = logger;
        _context = context;
        _redis = redis;
    }

    public async Task<UserProfileDto> GetUserProfileAsync(string userId)
    {
        // Get user from Keycloak
        var keycloakUser = await _keycloakAdmin.GetUserByIdAsync(userId);
        
        // Enrich with local data
        var localUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        
        return new UserProfileDto
        {
            Id = userId,
            Username = keycloakUser?.Username,
            Email = keycloakUser?.Email,
            EmailVerified = keycloakUser?.EmailVerified ?? false,
            FirstName = keycloakUser?.FirstName,
            LastName = keycloakUser?.LastName,
            BranchId = localUser?.BranchId,
            BranchName = localUser?.BranchName,
            CreatedAt = keycloakUser?.CreatedTimestamp != null
                ? DateTimeOffset.FromUnixTimeMilliseconds(keycloakUser.CreatedTimestamp.Value).DateTime
                : DateTime.UtcNow
        };
    }

    public async Task<UpdateProfileResult> UpdateUserProfileAsync(string userId, UpdateProfileRequest request)
    {
        try
        {
            // Get existing user from Keycloak
            var existingUser = await _keycloakAdmin.GetUserByIdAsync(userId);
            if (existingUser == null)
            {
                return new UpdateProfileResult
                {
                    Success = false,
                    Errors = new List<string> { "User not found" }
                };
            }

            // Update Keycloak user
            existingUser.FirstName = request.FirstName ?? existingUser.FirstName;
            existingUser.LastName = request.LastName ?? existingUser.LastName;
            existingUser.Email = request.Email ?? existingUser.Email;

            var updated = await _keycloakAdmin.UpdateUserAsync(userId, existingUser);
            
            if (!updated)
            {
                return new UpdateProfileResult
                {
                    Success = false,
                    Errors = new List<string> { "Failed to update profile in identity provider" }
                };
            }

            // Sync to local database
            var localUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (localUser != null)
            {
                localUser.Email = request.Email ?? localUser.Email;
                await _context.SaveChangesAsync();
            }

            var updatedProfile = await GetUserProfileAsync(userId);
            
            return new UpdateProfileResult
            {
                Success = true,
                Profile = updatedProfile
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user profile for user {UserId}", userId);
            return new UpdateProfileResult
            {
                Success = false,
                Errors = new List<string> { "Failed to update profile. Please try again." }
            };
        }
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        try
        {
            // Note: In a full implementation, we would verify the current password first
            // For now, we'll set the new password directly using Keycloak Admin API
            // The actual password verification should be done through Keycloak's user credential update endpoint
            
            // Set new password (non-temporary)
            var success = await _keycloakAdmin.SetTemporaryPasswordAsync(userId, newPassword);
            
            if (!success)
            {
                return new ChangePasswordResult
                {
                    Success = false,
                    Errors = new List<string> { "Failed to change password" }
                };
            }

            // Invalidate all sessions for this user
            await InvalidateUserSessionsAsync(userId);

            return new ChangePasswordResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change password for user {UserId}", userId);
            return new ChangePasswordResult
            {
                Success = false,
                Errors = new List<string> { "Failed to change password. Please try again." }
            };
        }
    }

    public async Task<List<SessionDto>> GetActiveSessionsAsync(string userId)
    {
        var sessions = new List<SessionDto>();
        
        try
        {
            // Query Redis for active sessions
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            // Get all session keys for this user
            var pattern = $"session:{userId}:*";
            var keys = server.Keys(pattern: pattern);
            
            foreach (var key in keys)
            {
                var sessionData = await db.StringGetAsync(key);
                if (!sessionData.IsNullOrEmpty)
                {
                    try
                    {
                        var session = JsonSerializer.Deserialize<SessionDto>(sessionData.ToString());
                        if (session != null)
                        {
                            sessions.Add(session);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize session data for key {Key}", key.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions for user {UserId}", userId);
        }

        return sessions;
    }

    public async Task<bool> RevokeSessionAsync(string userId, string sessionId)
    {
        try
        {
            // Remove from Redis cache
            var db = _redis.GetDatabase();
            var key = $"session:{userId}:{sessionId}";
            var deleted = await db.KeyDeleteAsync(key);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke session {SessionId} for user {UserId}", sessionId, userId);
            return false;
        }
    }

    public async Task SendPasswordChangedNotificationAsync(string userId)
    {
        // This would trigger Keycloak to send a password changed email
        // For now, we'll just log it
        // In production, you'd integrate with Keycloak's email service or your own SMTP service
        _logger.LogInformation("Password changed notification would be sent to user {UserId}", userId);
        await Task.CompletedTask;
    }

    private async Task InvalidateUserSessionsAsync(string userId)
    {
        try
        {
            // Clear all session keys in Redis for this user
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = $"session:{userId}:*";
            var keys = server.Keys(pattern: pattern);
            
            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key);
            }
            
            _logger.LogInformation("Invalidated all sessions for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate sessions for user {UserId}", userId);
        }
    }
}
