using System.Security.Claims;
using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/role-governance")]
[Authorize(Roles = "System Administrator,Compliance Officer")]
public class RoleManagementController : ControllerBase
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly ILogger<RoleManagementController> _logger;

    public RoleManagementController(IRoleManagementService roleManagementService, ILogger<RoleManagementController> logger)
    {
        _roleManagementService = roleManagementService;
        _logger = logger;
    }

    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoleDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoleDefinitions(CancellationToken cancellationToken)
    {
        var roles = await _roleManagementService.GetAllRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(UserRolesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(string userId, CancellationToken cancellationToken)
    {
        var result = await _roleManagementService.GetUserRolesAsync(userId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("users/{userId}/assign")]
    [Authorize(Roles = "System Administrator")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SodConflictResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignRole(string userId, [FromBody] RoleAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? adminId;

        try
        {
            var result = await _roleManagementService.AssignRoleAsync(
                userId,
                request.RoleName,
                request.ConfirmedSodOverride ?? false,
                adminId,
                adminName,
                cancellationToken);

            return Ok(new
            {
                message = "Role assigned successfully",
                roleAssignmentId = result.RoleAssignmentId
            });
        }
        catch (SodConflictException ex)
        {
            _logger.LogWarning(
                "Role assignment blocked for user {User} and role {Role}: {Message}",
                userId,
                request.RoleName,
                ex.Message);

            return Conflict(new SodConflictResponse(
                true,
                ex.ConflictingRoles,
                ex.Message,
                ex.Severity,
                !string.Equals(ex.Severity, "Critical", StringComparison.OrdinalIgnoreCase),
                "/api/admin/sod/exception-request"));
        }
    }

    [HttpPost("users/{userId}/remove")]
    [Authorize(Roles = "System Administrator")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveRole(string userId, [FromBody] RoleRemovalRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? adminId;

        await _roleManagementService.RemoveRoleAsync(userId, request.RoleName, adminId, adminName, request.Reason, cancellationToken);

        return Ok(new { message = "Role removed successfully" });
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(SodValidationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateSod([FromBody] SodValidationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleManagementService.ValidateSodAsync(request.UserId, request.ProposedRole, cancellationToken);
        return Ok(result);
    }

    [HttpGet("hierarchy")]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoleHierarchyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHierarchy(CancellationToken cancellationToken)
    {
        var hierarchy = await _roleManagementService.GetRoleHierarchyAsync(cancellationToken);
        return Ok(hierarchy);
    }

    [HttpGet("policies")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SodPolicyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicies(CancellationToken cancellationToken)
    {
        var policies = await _roleManagementService.GetPoliciesAsync(cancellationToken);
        return Ok(policies);
    }
}

