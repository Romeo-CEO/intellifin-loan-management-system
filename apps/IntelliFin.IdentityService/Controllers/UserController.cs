using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Tenant Plane User Management Controller
/// Customer organization operations for managing their own tenant users
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ITenantResolver tenantResolver,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Gets current user's profile
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMyProfile()
    {
        try
        {
            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var user = await _userService.GetUserByIdAsync(currentUserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user profile");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates current user's profile
    /// </summary>
    [HttpPut("me")]
    public async Task<ActionResult<UserResponse>> UpdateMyProfile([FromBody] UpdateUserRequest request)
    {
        try
        {
            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Tenant users cannot change their active status or certain administrative fields
            request.IsActive = null;

            var user = await _userService.UpdateUserAsync(currentUserId, request);
            
            _logger.LogInformation("Updated profile for user {UserId}", currentUserId);
            
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid profile update request: {Error}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all users in current tenant (admin permission required)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetTenantUsers()
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            // TODO: Add permission check for user management
            var users = await _userService.GetAllUsersAsync();
            
            _logger.LogInformation("Retrieved users for tenant {TenantId}", tenantId);
            
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant users");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets active users in current tenant
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetActiveTenantUsers()
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var users = await _userService.GetActiveUsersAsync();
            
            _logger.LogInformation("Retrieved active users for tenant {TenantId}", tenantId);
            
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active tenant users");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets user by ID (within current tenant only)
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserResponse>> GetUserById(string userId)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // TODO: Verify user belongs to current tenant
            
            _logger.LogInformation("Retrieved user {UserId} for tenant {TenantId}", userId, tenantId);
            
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new user in current tenant (admin permission required)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // TODO: Add permission check for user creation
            // TODO: Ensure user is created within current tenant context

            var user = await _userService.CreateUserAsync(request);
            
            _logger.LogInformation("Created user {UserId} in tenant {TenantId}", user.Id, tenantId);
            
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
    /// Updates user information (admin permission required, within tenant only)
    /// </summary>
    [HttpPut("{userId}")]
    public async Task<ActionResult<UserResponse>> UpdateUser(string userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // TODO: Add permission check for user management
            // TODO: Verify user belongs to current tenant

            var user = await _userService.UpdateUserAsync(userId, request);
            
            _logger.LogInformation("Updated user {UserId} in tenant {TenantId}", userId, tenantId);
            
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
    /// Deactivates a user (admin permission required)
    /// </summary>
    [HttpPost("{userId}/deactivate")]
    public async Task<ActionResult> DeactivateUser(string userId)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            // TODO: Add permission check for user management
            // TODO: Verify user belongs to current tenant

            var request = new UpdateUserRequest { IsActive = false };
            await _userService.UpdateUserAsync(userId, request);
            
            _logger.LogInformation("Deactivated user {UserId} in tenant {TenantId}", userId, tenantId);
            
            return Ok(new { message = "User deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Changes current user's password
    /// </summary>
    [HttpPost("me/change-password")]
    public async Task<ActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Current password and new password are required" });
            }

            var result = await _userService.UpdatePasswordAsync(currentUserId, request.CurrentPassword, request.NewPassword);
            
            if (!result)
            {
                return BadRequest(new { message = "Invalid current password or password update failed" });
            }

            _logger.LogInformation("Password changed for user {UserId}", currentUserId);
            
            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets current user's roles
    /// </summary>
    [HttpGet("me/roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetMyRoles()
    {
        try
        {
            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var roles = await _userService.GetUserRolesAsync(currentUserId);
            
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user roles");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets current user's permissions
    /// </summary>
    [HttpGet("me/permissions")]
    public async Task<ActionResult<IEnumerable<string>>> GetMyPermissions()
    {
        try
        {
            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var permissions = await _userService.GetUserPermissionsAsync(currentUserId);
            
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user permissions");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a role to a user (admin permission required)
    /// </summary>
    [HttpPost("{userId}/roles/{roleId}")]
    public async Task<ActionResult> AssignRole(string userId, string roleId)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            // TODO: Add permission check for role assignment
            // TODO: Verify user and role belong to current tenant

            var assignedBy = _tenantResolver.GetCurrentUserId() ?? "system";
            var result = await _userService.AssignRoleAsync(userId, roleId, assignedBy);

            if (!result.IsAllowed)
            {
                return Conflict(new
                {
                    message = "Role assignment blocked due to Segregation of Duties conflict.",
                    conflicts = result.Conflicts.Select(c => new
                    {
                        c.RuleId,
                        c.RuleName,
                        Enforcement = c.Enforcement.ToString(),
                        ConflictingPermissions = c.ConflictingPermissions,
                        TriggeringPermissions = c.TriggeringPermissions
                    })
                });
            }

            if (!result.WasApplied)
            {
                return StatusCode(500, new { message = "Failed to assign role" });
            }

            if (result.HasWarnings)
            {
                _logger.LogWarning(
                    "Role {RoleId} assigned to user {UserId} with SoD warnings {Warnings}",
                    roleId,
                    userId,
                    string.Join(", ", result.Conflicts.Select(c => c.RuleName)));

                return Ok(new
                {
                    message = "Role assigned with warnings",
                    warnings = result.Conflicts.Select(c => new
                    {
                        c.RuleId,
                        c.RuleName,
                        Enforcement = c.Enforcement.ToString(),
                        ConflictingPermissions = c.ConflictingPermissions,
                        TriggeringPermissions = c.TriggeringPermissions
                    })
                });
            }

            _logger.LogInformation("Assigned role {RoleId} to user {UserId} in tenant {TenantId}", roleId, userId, tenantId);

            return Ok(new { message = "Role assigned successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Role assignment failed due to missing entity for user {UserId} or role {RoleId}", userId, roleId);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Role assignment invalid for user {UserId} and role {RoleId}", userId, roleId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a role from a user (admin permission required)
    /// </summary>
    [HttpDelete("{userId}/roles/{roleId}")]
    public async Task<ActionResult> RemoveRole(string userId, string roleId)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            // TODO: Add permission check for role management
            // TODO: Verify user and role belong to current tenant

            var result = await _userService.RemoveRoleAsync(userId, roleId);
            
            if (!result)
            {
                return NotFound(new { message = "Role assignment not found" });
            }

            _logger.LogInformation("Removed role {RoleId} from user {UserId} in tenant {TenantId}", roleId, userId, tenantId);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for password change
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}