using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

/// <summary>
/// Platform Plane User Management Controller
/// IntelliFin internal team operations for managing users across all tenants
/// </summary>
[ApiController]
[Route("api/platform/users")]
[Produces("application/json")]
public class PlatformUserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<PlatformUserController> _logger;

    public PlatformUserController(
        IUserService userService,
        ITenantResolver tenantResolver,
        ILogger<PlatformUserController> logger)
    {
        _userService = userService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Gets all users across all tenants (Platform Plane only)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAllUsers()
    {
        try
        {
            // Verify Platform Plane access
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var users = await _userService.GetAllUsersAsync();
            
            _logger.LogInformation("Retrieved {UserCount} users for platform plane", users.Count());
            
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets active users across all tenants
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetActiveUsers()
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var users = await _userService.GetActiveUsersAsync();
            
            _logger.LogInformation("Retrieved {UserCount} active users for platform plane", users.Count());
            
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active users");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets user by ID (Platform Plane can access any user)
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserResponse>> GetUserById(string userId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            _logger.LogInformation("Retrieved user {UserId} for platform plane", userId);
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new user (Platform Plane can create users in any tenant)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.CreateUserAsync(request);
            
            _logger.LogInformation("Created user {UserId} by platform admin", user.Id);
            
            return CreatedAtAction(nameof(GetUserById), new { userId = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid user creation request: {Error}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates user information (Platform Plane can update any user)
    /// </summary>
    [HttpPut("{userId}")]
    public async Task<ActionResult<UserResponse>> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.UpdateUserAsync(userId, request);
            
            _logger.LogInformation("Updated user {UserId} by platform admin", userId);
            
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid user update request: {Error}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a user (Platform Plane only - soft delete)
    /// </summary>
    [HttpDelete("{userId}")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var result = await _userService.DeleteUserAsync(userId);
            if (!result)
            {
                return NotFound(new { message = "User not found" });
            }

            _logger.LogInformation("Deleted user {UserId} by platform admin", userId);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a role to a user (Platform Plane can assign any role to any user)
    /// </summary>
    [HttpPost("{userId}/roles/{roleId}")]
    public async Task<ActionResult> AssignRole(string userId, string roleId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var assignedBy = _tenantResolver.GetCurrentUserId() ?? "system";
            var result = await _userService.AssignRoleAsync(userId, roleId, assignedBy);
            
            if (!result)
            {
                return BadRequest(new { message = "Failed to assign role" });
            }

            _logger.LogInformation("Assigned role {RoleId} to user {UserId} by platform admin", roleId, userId);
            
            return Ok(new { message = "Role assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a role from a user (Platform Plane only)
    /// </summary>
    [HttpDelete("{userId}/roles/{roleId}")]
    public async Task<ActionResult> RemoveRole(string userId, string roleId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var result = await _userService.RemoveRoleAsync(userId, roleId);
            
            if (!result)
            {
                return NotFound(new { message = "Role assignment not found" });
            }

            _logger.LogInformation("Removed role {RoleId} from user {UserId} by platform admin", roleId, userId);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets user roles (Platform Plane can view any user's roles)
    /// </summary>
    [HttpGet("{userId}/roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserRoles(string userId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var roles = await _userService.GetUserRolesAsync(userId);
            
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets user permissions (Platform Plane can view any user's permissions)
    /// </summary>
    [HttpGet("{userId}/permissions")]
    public async Task<ActionResult<IEnumerable<string>>> GetUserPermissions(string userId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var permissions = await _userService.GetUserPermissionsAsync(userId);
            
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Resets user password (Platform Plane emergency function)
    /// </summary>
    [HttpPost("{userId}/reset-password")]
    public async Task<ActionResult> ResetPassword(string userId, [FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "New password is required" });
            }

            var result = await _userService.ResetPasswordAsync(userId, request.NewPassword);
            
            if (!result)
            {
                return BadRequest(new { message = "Failed to reset password" });
            }

            _logger.LogInformation("Reset password for user {UserId} by platform admin", userId);
            
            return Ok(new { message = "Password reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for password reset
/// </summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}