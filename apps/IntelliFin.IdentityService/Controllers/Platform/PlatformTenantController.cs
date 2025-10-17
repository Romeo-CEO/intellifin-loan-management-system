using System.Net.Mime;
using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

[ApiController]
[Route("platform/v1/tenants")]
[Authorize(Policy = SystemPermissions.PlatformTenantsManage)]
[Produces(MediaTypeNames.Application.Json)]
public class PlatformTenantController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<PlatformTenantController> _logger;

    public PlatformTenantController(ITenantService tenantService, ILogger<PlatformTenantController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new tenant
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] TenantCreateRequest request, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.CreateTenantAsync(request, ct);
            return CreatedAtAction(nameof(GetTenants), new { tenantId = tenant.TenantId }, tenant);
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate code or not found scenarios are surfaced as InvalidOperationException from service.
            _logger.LogWarning(ex, "Error creating tenant");
            var pd = new ProblemDetails
            {
                Title = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            };
            return Conflict(pd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating tenant");
            var pd = new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while creating the tenant.",
                Status = StatusCodes.Status500InternalServerError
            };
            return StatusCode(StatusCodes.Status500InternalServerError, pd);
        }
    }

    /// <summary>
    /// List tenants (paged)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await _tenantService.ListTenantsAsync(page, pageSize, isActive, ct);
        return Ok(result);
    }

    /// <summary>
    /// Assign a user to a tenant (idempotent)
    /// </summary>
    [HttpPost("{tenantId:guid}/users")]
    public async Task<IActionResult> AssignUser(Guid tenantId, [FromBody] UserAssignmentRequest request, CancellationToken ct)
    {
        try
        {
            await _tenantService.AssignUserToTenantAsync(tenantId, request.UserId, request.Role, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error assigning user to tenant");
            var pd = new ProblemDetails
            {
                Title = "Bad Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            };
            return BadRequest(pd);
        }
    }

    /// <summary>
    /// Remove a user from a tenant (idempotent)
    /// </summary>
    [HttpDelete("{tenantId:guid}/users/{userId}")]
    public async Task<IActionResult> RemoveUser(Guid tenantId, string userId, CancellationToken ct)
    {
        await _tenantService.RemoveUserFromTenantAsync(tenantId, userId, ct);
        return NoContent();
    }
}
