using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Constants;

namespace IntelliFin.IdentityService.Controllers.Platform;

/// <summary>
/// Platform Plane API for permission catalog management
/// Available to IntelliFin internal team with PlatformAdmin role
/// Routes: /platform/v1/permissions/*
/// </summary>
[ApiController]
[Route("platform/v1/permissions")]
[Authorize(Roles = "PlatformAdmin")]
[Produces("application/json")]
public class PlatformPermissionCatalogController : ControllerBase
{
    private readonly IPermissionCatalogService _permissionCatalogService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<PlatformPermissionCatalogController> _logger;

    public PlatformPermissionCatalogController(
        IPermissionCatalogService permissionCatalogService,
        ITenantResolver tenantResolver,
        ILogger<PlatformPermissionCatalogController> logger)
    {
        _permissionCatalogService = permissionCatalogService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Get all system permissions with optional filtering
    /// Platform Plane: Access to all permissions including deprecated ones
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemPermission[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemPermission[]>> GetAllPermissions(
        [FromQuery] string? category = null,
        [FromQuery] PermissionRiskLevel? riskLevel = null,
        [FromQuery] bool includeDeprecated = true,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            _logger.LogWarning("Platform permissions access denied for non-platform user: {UserId}", _tenantResolver.GetCurrentUserId());
            return Forbid();
        }

        try
        {
            var permissions = await _permissionCatalogService.GetAllPermissionsAsync(
                category, riskLevel, includeDeprecated, cancellationToken);

            _logger.LogDebug("Retrieved {Count} permissions for platform user with filters - Category: {Category}, RiskLevel: {RiskLevel}", 
                permissions.Length, category, riskLevel);

            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for platform user");
            return StatusCode(500, "An error occurred while retrieving permissions");
        }
    }

    /// <summary>
    /// Get permission by ID with full metadata
    /// Platform Plane: Access to any permission including deprecated ones
    /// </summary>
    [HttpGet("{permissionId}")]
    [ProducesResponseType(typeof(SystemPermission), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemPermission>> GetPermissionById(
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        try
        {
            var permission = await _permissionCatalogService.GetPermissionByIdAsync(permissionId, cancellationToken);
            if (permission == null)
            {
                _logger.LogWarning("Permission not found: {PermissionId}", permissionId);
                return NotFound();
            }

            return Ok(permission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission {PermissionId}", permissionId);
            return StatusCode(500, "An error occurred while retrieving the permission");
        }
    }

    /// <summary>
    /// Get permission categories with statistics
    /// Platform Plane: Access to all categories including restricted ones
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(PermissionCategory[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionCategory[]>> GetPermissionCategories(
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        try
        {
            var categories = await _permissionCatalogService.GetPermissionCategoriesAsync(null, cancellationToken);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission categories for platform user");
            return StatusCode(500, "An error occurred while retrieving permission categories");
        }
    }

    /// <summary>
    /// Search permissions across the entire catalog
    /// Platform Plane: Search includes all permissions without tenant restrictions
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(SystemPermission[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemPermission[]>> SearchPermissions(
        [FromQuery] string query,
        [FromQuery] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
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
            var permissions = await _permissionCatalogService.SearchPermissionsAsync(
                query, null, maxResults, cancellationToken);

            _logger.LogDebug("Platform search returned {Count} permissions for query: {Query}", 
                permissions.Length, query);

            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching permissions for platform user with query: {Query}", query);
            return StatusCode(500, "An error occurred while searching permissions");
        }
    }

    /// <summary>
    /// Create a new system permission
    /// Platform Plane Only: Only IntelliFin team can create new permissions
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SystemPermission), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemPermission>> CreatePermission(
        [FromBody] SystemPermission permission,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
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

            var createdPermission = await _permissionCatalogService.CreatePermissionAsync(
                permission, userId, cancellationToken);

            _logger.LogInformation("Permission created by platform user {UserId}: {PermissionId}", 
                userId, createdPermission.Id);

            return CreatedAtAction(
                nameof(GetPermissionById),
                new { permissionId = createdPermission.Id },
                createdPermission);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid permission creation attempt: {Error}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission for platform user");
            return StatusCode(500, "An error occurred while creating the permission");
        }
    }

    /// <summary>
    /// Update permission metadata
    /// Platform Plane Only: Only IntelliFin team can update permissions
    /// </summary>
    [HttpPut("{permissionId}")]
    [ProducesResponseType(typeof(SystemPermission), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemPermission>> UpdatePermission(
        string permissionId,
        [FromBody] SystemPermission permission,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
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

            var updatedPermission = await _permissionCatalogService.UpdatePermissionAsync(
                permissionId, permission, userId, cancellationToken);

            _logger.LogInformation("Permission updated by platform user {UserId}: {PermissionId}", 
                userId, permissionId);

            return Ok(updatedPermission);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid permission update attempt for {PermissionId}: {Error}", permissionId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permission {PermissionId} for platform user", permissionId);
            return StatusCode(500, "An error occurred while updating the permission");
        }
    }

    /// <summary>
    /// Deprecate a permission (soft delete)
    /// Platform Plane Only: Only IntelliFin team can deprecate permissions
    /// </summary>
    [HttpDelete("{permissionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeprecatePermission(
        string permissionId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
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

            var success = await _permissionCatalogService.DeprecatePermissionAsync(
                permissionId, userId, cancellationToken);

            if (!success)
            {
                return NotFound();
            }

            _logger.LogInformation("Permission deprecated by platform user {UserId}: {PermissionId}", 
                userId, permissionId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deprecating permission {PermissionId} for platform user", permissionId);
            return StatusCode(500, "An error occurred while deprecating the permission");
        }
    }

    /// <summary>
    /// Get permission usage analytics across all tenants
    /// Platform Plane Only: Analytics across the entire platform
    /// </summary>
    [HttpGet("analytics/usage")]
    [ProducesResponseType(typeof(PermissionUsageStats[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionUsageStats[]>> GetPermissionUsageStats(
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        try
        {
            var stats = await _permissionCatalogService.GetPermissionUsageStatsAsync(cancellationToken);

            _logger.LogDebug("Retrieved usage stats for {Count} permissions for platform user", stats.Length);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission usage stats for platform user");
            return StatusCode(500, "An error occurred while retrieving usage statistics");
        }
    }

    /// <summary>
    /// Analyze permission deployment impact across tenants
    /// Platform Plane Only: Impact analysis for permission changes
    /// </summary>
    [HttpPost("analytics/impact")]
    [ProducesResponseType(typeof(PermissionImpactAnalysis), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionImpactAnalysis>> AnalyzePermissionImpact(
        [FromBody] string[] permissionIds,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        if (permissionIds == null || permissionIds.Length == 0)
        {
            return BadRequest("Permission IDs are required for impact analysis");
        }

        try
        {
            var analysis = await _permissionCatalogService.AnalyzePermissionImpactAsync(permissionIds, cancellationToken);

            _logger.LogDebug("Analyzed impact for {Count} permissions affecting {AffectedTenants} tenants", 
                permissionIds.Length, analysis.AffectedTenants);

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing permission impact for platform user");
            return StatusCode(500, "An error occurred while analyzing permission impact");
        }
    }
}