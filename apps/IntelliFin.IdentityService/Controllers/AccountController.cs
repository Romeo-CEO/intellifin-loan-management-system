using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Account management endpoints for self-service operations
/// </summary>
[ApiController]
[Route("api/auth")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountManagementService _accountService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAccountManagementService accountService,
        IAuditService auditService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var profile = await _accountService.GetUserProfileAsync(userId);
        return Ok(profile);
    }

    /// <summary>
    /// Update current user profile
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _accountService.UpdateUserProfileAsync(userId, request);
        
        if (!result.Success)
        {
            return BadRequest(new { errors = result.Errors });
        }

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "ProfileUpdated",
            Entity = "User",
            EntityId = userId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Details = new Dictionary<string, object>
            {
                ["FirstName"] = request.FirstName ?? "",
                ["LastName"] = request.LastName ?? "",
                ["Email"] = request.Email ?? ""
            },
            Success = true
        });

        return Ok(result.Profile);
    }

    /// <summary>
    /// Change password (requires current password)
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _accountService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        
        if (!result.Success)
        {
            return BadRequest(new { errors = result.Errors });
        }

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "PasswordChanged",
            Entity = "User",
            EntityId = userId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Success = true
        });

        // Send notification email
        await _accountService.SendPasswordChangedNotificationAsync(userId);

        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// List active sessions for current user
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<SessionDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSessions()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var sessions = await _accountService.GetActiveSessionsAsync(userId);
        return Ok(sessions);
    }

    /// <summary>
    /// Revoke a specific session
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _accountService.RevokeSessionAsync(userId, sessionId);
        
        if (!result)
        {
            return NotFound(new { message = "Session not found or already revoked" });
        }

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = userId,
            Action = "SessionRevoked",
            Entity = "Session",
            EntityId = sessionId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Success = true
        });

        return NoContent();
    }
}
