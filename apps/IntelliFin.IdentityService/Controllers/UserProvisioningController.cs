using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Controller for managing user provisioning to Keycloak
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "system-admin")]
public class UserProvisioningController : ControllerBase
{
    private readonly IUserProvisioningService _provisioningService;
    private readonly ILogger<UserProvisioningController> _logger;

    public UserProvisioningController(
        IUserProvisioningService provisioningService,
        ILogger<UserProvisioningController> logger)
    {
        _provisioningService = provisioningService;
        _logger = logger;
    }

    /// <summary>
    /// Provision a single user to Keycloak by user ID
    /// </summary>
    /// <param name="userId">The user ID to provision</param>
    [HttpPost("provision-user/{userId}")]
    public async Task<ActionResult<UserProvisioningResponse>> ProvisionUser(string userId)
    {
        _logger.LogInformation("Admin {AdminUser} requested provisioning for user {UserId}", 
            User.Identity?.Name, userId);

        var result = await _provisioningService.ProvisionUserAsync(userId);

        if (result.Success)
        {
            return Ok(new UserProvisioningResponse
            {
                Success = true,
                KeycloakUserId = result.KeycloakUserId,
                AlreadyExisted = result.AlreadyExisted,
                Message = result.AlreadyExisted 
                    ? "User already exists in Keycloak" 
                    : "User successfully provisioned to Keycloak",
                TemporaryPassword = result.TemporaryPassword
            });
        }

        return BadRequest(new UserProvisioningResponse
        {
            Success = false,
            Message = result.ErrorMessage ?? "Failed to provision user"
        });
    }

    /// <summary>
    /// Provision a single user to Keycloak by email
    /// </summary>
    /// <param name="request">Request containing user email</param>
    [HttpPost("provision-user-by-email")]
    public async Task<ActionResult<UserProvisioningResponse>> ProvisionUserByEmail(
        [FromBody] ProvisionByEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new UserProvisioningResponse
            {
                Success = false,
                Message = "Email is required"
            });
        }

        _logger.LogInformation("Admin {AdminUser} requested provisioning for user with email {Email}", 
            User.Identity?.Name, request.Email);

        var result = await _provisioningService.ProvisionUserByEmailAsync(request.Email);

        if (result.Success)
        {
            return Ok(new UserProvisioningResponse
            {
                Success = true,
                KeycloakUserId = result.KeycloakUserId,
                AlreadyExisted = result.AlreadyExisted,
                Message = result.AlreadyExisted 
                    ? "User already exists in Keycloak" 
                    : "User successfully provisioned to Keycloak",
                TemporaryPassword = result.TemporaryPassword
            });
        }

        return BadRequest(new UserProvisioningResponse
        {
            Success = false,
            Message = result.ErrorMessage ?? "Failed to provision user"
        });
    }

    /// <summary>
    /// Provision all users to Keycloak (bulk operation)
    /// </summary>
    [HttpPost("provision-all-users")]
    public async Task<ActionResult<BulkProvisioningResponse>> ProvisionAllUsers()
    {
        _logger.LogWarning("Admin {AdminUser} initiated bulk user provisioning to Keycloak", 
            User.Identity?.Name);

        var result = await _provisioningService.ProvisionAllUsersAsync();

        return Ok(new BulkProvisioningResponse
        {
            TotalUsers = result.TotalUsers,
            SuccessfulProvisions = result.SuccessfulProvisions,
            FailedProvisions = result.FailedProvisions,
            SuccessRate = result.SuccessRate,
            Errors = result.Errors,
            Message = $"Bulk provisioning complete: {result.SuccessfulProvisions}/{result.TotalUsers} users provisioned successfully"
        });
    }

    /// <summary>
    /// Check if a user is already provisioned in Keycloak
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    [HttpGet("check-user/{userId}")]
    public async Task<ActionResult<CheckProvisioningResponse>> CheckUserProvisioned(string userId)
    {
        var isProvisioned = await _provisioningService.IsUserProvisionedAsync(userId);

        return Ok(new CheckProvisioningResponse
        {
            UserId = userId,
            IsProvisioned = isProvisioned,
            Message = isProvisioned 
                ? "User is provisioned in Keycloak" 
                : "User is not provisioned in Keycloak"
        });
    }
}

/// <summary>
/// Response for user provisioning operation
/// </summary>
public class UserProvisioningResponse
{
    public bool Success { get; set; }
    public string? KeycloakUserId { get; set; }
    public string? TemporaryPassword { get; set; }
    public bool AlreadyExisted { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response for bulk provisioning operation
/// </summary>
public class BulkProvisioningResponse
{
    public int TotalUsers { get; set; }
    public int SuccessfulProvisions { get; set; }
    public int FailedProvisions { get; set; }
    public double SuccessRate { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }
}

/// <summary>
/// Response for checking provisioning status
/// </summary>
public class CheckProvisioningResponse
{
    public string? UserId { get; set; }
    public bool IsProvisioned { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Request to provision user by email
/// </summary>
public class ProvisionByEmailRequest
{
    public string Email { get; set; } = string.Empty;
}
