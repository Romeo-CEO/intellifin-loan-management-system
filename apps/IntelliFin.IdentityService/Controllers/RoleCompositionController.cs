using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Tenant Plane API for role composition and management
/// Routes: /v1/roles/*
/// </summary>
[ApiController]
[Route("v1/roles")]
[Authorize]
[Produces("application/json")]
public class RoleCompositionController : ControllerBase
{
    private readonly IRoleCompositionService _roleCompositionService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<RoleCompositionController> _logger;

    public RoleCompositionController(
        IRoleCompositionService roleCompositionService,
        ITenantResolver tenantResolver,
        ILogger<RoleCompositionController> logger)
    {
        _roleCompositionService = roleCompositionService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Create a new custom role for the tenant
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationRole>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("Unable to identify current user");
            }

            var role = await _roleCompositionService.CreateRoleAsync(request, tenantId, userId, cancellationToken);

            _logger.LogInformation("Created role {RoleName} ({RoleId}) for tenant {TenantId} by user {UserId}",
                role.Name, role.Id, tenantId, userId);

            return CreatedAtAction(
                nameof(GetRoleById),
                new { roleId = role.Id },
                role);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while creating the role");
        }
    }

    /// <summary>
    /// Update role metadata (name, description, category)
    /// </summary>
    [HttpPut("{roleId}")]
    [ProducesResponseType(typeof(ApplicationRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationRole>> UpdateRole(
        string roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("Unable to identify current user");
            }

            var role = await _roleCompositionService.UpdateRoleAsync(roleId, request, tenantId, userId, cancellationToken);
            return Ok(role);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while updating the role");
        }
    }

    /// <summary>
    /// Delete a role (validates no user assignments exist)
    /// </summary>
    [HttpDelete("{roleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteRole(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var userId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("Unable to identify current user");
            }

            var success = await _roleCompositionService.DeleteRoleAsync(roleId, tenantId, userId, cancellationToken);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while deleting the role");
        }
    }

    /// <summary>
    /// Get all roles for the current tenant with summary information
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantRolesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantRolesResponse>> GetTenantRoles(
        [FromQuery] bool includeInactive = false,
        [FromQuery] RoleCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            ApplicationRole[] roles;
            if (category.HasValue)
            {
                roles = await _roleCompositionService.GetRolesByCategoryAsync(tenantId, category.Value, includeInactive, cancellationToken);
            }
            else
            {
                roles = await _roleCompositionService.GetTenantRolesAsync(tenantId, includeInactive, cancellationToken);
            }

            var summary = await _roleCompositionService.GetTenantRoleSummaryAsync(tenantId, cancellationToken);

            var response = new TenantRolesResponse
            {
                Roles = roles.Select(r => new RoleSummaryInfo
                {
                    RoleId = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Category = r.Category,
                    UserCount = r.UserCount,
                    PermissionCount = r.Claims?.Count(c => c.ClaimType == "permission") ?? 0,
                    LastModified = r.UpdatedAt ?? r.CreatedAt,
                    ComplianceStatus = r.ComplianceScore >= 70 ? "compliant" : "warning",
                    ComplianceIssues = r.ComplianceScore < 70 ? new[] { "Below compliance threshold" } : Array.Empty<string>(),
                    IsActive = r.IsActive
                }).ToArray(),
                Summary = summary,
                TenantContext = new TenantRoleContext
                {
                    TenantId = tenantId,
                    CanCreateRoles = true,
                    MaxRoles = 20,
                    RolesUsed = roles.Length,
                    SubscriptionTier = "Professional", // Would be retrieved from tenant info
                    EnabledFeatures = new[] { "loan_origination", "client_management", "basic_reporting" }
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while retrieving roles");
        }
    }

    /// <summary>
    /// Get detailed role information including all permissions
    /// </summary>
    [HttpGet("{roleId}")]
    [ProducesResponseType(typeof(ApplicationRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationRole>> GetRoleById(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var role = await _roleCompositionService.GetRoleByIdAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                return NotFound();
            }

            return Ok(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while retrieving the role");
        }
    }

    /// <summary>
    /// Add permissions to a role
    /// </summary>
    [HttpPost("{roleId}/permissions")]
    [ProducesResponseType(typeof(RolePermissionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RolePermissionResult>> AddPermissionsToRole(
        string roleId,
        [FromBody] AddPermissionsToRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("Unable to identify current user");
            }

            var result = await _roleCompositionService.AddPermissionsToRoleAsync(roleId, request, tenantId, userId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding permissions to role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while adding permissions");
        }
    }

    /// <summary>
    /// Remove a specific permission from a role
    /// </summary>
    [HttpDelete("{roleId}/permissions/{permissionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemovePermissionFromRole(
        string roleId,
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var userId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("Unable to identify current user");
            }

            var success = await _roleCompositionService.RemovePermissionFromRoleAsync(roleId, permissionId, tenantId, userId, cancellationToken);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {PermissionId} from role {RoleId} for tenant {TenantId}", 
                permissionId, roleId, tenantId);
            return StatusCode(500, "An error occurred while removing permission");
        }
    }

    /// <summary>
    /// Bulk update role permissions (replaces all existing)
    /// </summary>
    [HttpPut("{roleId}/permissions")]
    [ProducesResponseType(typeof(RolePermissionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RolePermissionResult>> BulkUpdateRolePermissions(
        string roleId,
        [FromBody] BulkUpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("Unable to identify current user");
            }

            var result = await _roleCompositionService.BulkUpdateRolePermissionsAsync(roleId, request, tenantId, userId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating permissions for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while updating permissions");
        }
    }

    /// <summary>
    /// Get all permissions assigned to a role
    /// </summary>
    [HttpGet("{roleId}/permissions")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<string[]>> GetRolePermissions(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var permissions = await _roleCompositionService.GetRolePermissionsAsync(roleId, tenantId, cancellationToken);
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while retrieving permissions");
        }
    }

    /// <summary>
    /// Validate role for compliance and segregation of duties
    /// </summary>
    [HttpPost("{roleId}/validate")]
    [ProducesResponseType(typeof(ComplianceValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ComplianceValidationResult>> ValidateRoleCompliance(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var result = await _roleCompositionService.ValidateRoleComplianceAsync(roleId, tenantId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compliance for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while validating compliance");
        }
    }

    /// <summary>
    /// Check all tenant roles against BoZ compliance requirements
    /// </summary>
    [HttpGet("compliance-check")]
    [ProducesResponseType(typeof(TenantComplianceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantComplianceReport>> CheckTenantRoleCompliance(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var validationResults = await _roleCompositionService.CheckTenantRoleComplianceAsync(tenantId, cancellationToken);
            var roles = await _roleCompositionService.GetTenantRolesAsync(tenantId, false, cancellationToken);

            var report = new TenantComplianceReport
            {
                TenantId = tenantId,
                TotalRoles = roles.Length,
                CompliantRoles = validationResults.Count(v => v.IsCompliant),
                AverageComplianceScore = validationResults.Length > 0 ? (int)validationResults.Average(v => v.OverallScore) : 0,
                RoleValidations = validationResults.Select((v, i) => new RoleComplianceInfo
                {
                    RoleId = roles[i].Id,
                    RoleName = roles[i].Name,
                    ComplianceScore = v.OverallScore,
                    IsCompliant = v.IsCompliant,
                    IssueCount = v.ComplianceIssues.Length,
                    WarningCount = v.Warnings.Length,
                    LastValidated = v.ValidatedAt
                }).ToArray(),
                OverallStatus = validationResults.All(v => v.IsCompliant) ? "compliant" : "issues_found",
                LastChecked = DateTime.UtcNow
            };

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking compliance for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while checking compliance");
        }
    }

    /// <summary>
    /// Preview role capabilities and potential issues before assignment
    /// </summary>
    [HttpPost("{roleId}/preview")]
    [ProducesResponseType(typeof(RolePreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RolePreviewResult>> PreviewRole(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var preview = await _roleCompositionService.PreviewRoleAsync(roleId, tenantId, cancellationToken);
            return Ok(preview);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return StatusCode(500, "An error occurred while previewing the role");
        }
    }

    /// <summary>
    /// Search roles within the tenant
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApplicationRole[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationRole[]>> SearchRoles(
        [FromQuery] string query,
        [FromQuery] RoleCategory? category = null,
        [FromQuery] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query is required");
        }

        if (maxResults <= 0 || maxResults > 100)
        {
            return BadRequest("Max results must be between 1 and 100");
        }

        try
        {
            var roles = await _roleCompositionService.SearchTenantRolesAsync(tenantId, query, category, maxResults, cancellationToken);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching roles for tenant {TenantId} with query: {Query}", tenantId, query);
            return StatusCode(500, "An error occurred while searching roles");
        }
    }
}

/// <summary>
/// Response model for tenant roles listing
/// </summary>
public class TenantRolesResponse
{
    public RoleSummaryInfo[] Roles { get; set; } = Array.Empty<RoleSummaryInfo>();
    public TenantRoleSummary Summary { get; set; } = new();
    public TenantRoleContext TenantContext { get; set; } = new();
}

/// <summary>
/// Summary information for a role in listings
/// </summary>
public class RoleSummaryInfo
{
    public string RoleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoleCategory Category { get; set; }
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
    public DateTime LastModified { get; set; }
    public string ComplianceStatus { get; set; } = string.Empty;
    public string[] ComplianceIssues { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
}

/// <summary>
/// Tenant compliance report
/// </summary>
public class TenantComplianceReport
{
    public string TenantId { get; set; } = string.Empty;
    public int TotalRoles { get; set; }
    public int CompliantRoles { get; set; }
    public int AverageComplianceScore { get; set; }
    public RoleComplianceInfo[] RoleValidations { get; set; } = Array.Empty<RoleComplianceInfo>();
    public string OverallStatus { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// Compliance information for a specific role
/// </summary>
public class RoleComplianceInfo
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int ComplianceScore { get; set; }
    public bool IsCompliant { get; set; }
    public int IssueCount { get; set; }
    public int WarningCount { get; set; }
    public DateTime LastValidated { get; set; }
}