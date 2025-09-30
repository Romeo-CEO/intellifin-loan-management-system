using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Tenant Plane API for permission discovery and validation
/// Available to tenant organizations for role composition
/// Routes: /v1/permissions/*
/// </summary>
[ApiController]
[Route("v1/permissions")]
[Authorize]
[Produces("application/json")]
public class PermissionCatalogController : ControllerBase
{
    private readonly IPermissionCatalogService _permissionCatalogService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<PermissionCatalogController> _logger;

    public PermissionCatalogController(
        IPermissionCatalogService permissionCatalogService,
        ITenantResolver tenantResolver,
        ILogger<PermissionCatalogController> logger)
    {
        _permissionCatalogService = permissionCatalogService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Get permissions available to the current tenant
    /// Filtered by subscription tier and enabled features
    /// Excludes deprecated permissions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemPermission[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemPermission[]>> GetTenantAvailablePermissions(
        [FromQuery] string? category = null,
        [FromQuery] PermissionRiskLevel? riskLevel = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("Tenant permissions access denied for user without tenant context: {UserId}", 
                _tenantResolver.GetCurrentUserId());
            return Forbid();
        }

        try
        {
            SystemPermission[] permissions;

            if (category != null || riskLevel != null)
            {
                // Get all tenant permissions first, then filter
                var allPermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(
                    tenantId, cancellationToken);

                permissions = allPermissions
                    .Where(p => category == null || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .Where(p => riskLevel == null || p.RiskLevel == riskLevel)
                    .ToArray();
            }
            else
            {
                permissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(
                    tenantId, cancellationToken);
            }

            _logger.LogDebug("Retrieved {Count} available permissions for tenant {TenantId} with filters - Category: {Category}, RiskLevel: {RiskLevel}", 
                permissions.Length, tenantId, category, riskLevel);

            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available permissions for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while retrieving available permissions");
        }
    }

    /// <summary>
    /// Get permission by ID if available to current tenant
    /// Validates tenant access and subscription tier
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
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var permission = await _permissionCatalogService.GetPermissionByIdAsync(permissionId, cancellationToken);
            if (permission == null)
            {
                return NotFound();
            }

            // Validate tenant access to this permission
            var isAvailable = await _permissionCatalogService.IsPermissionAvailableToTenantAsync(
                permissionId, tenantId, cancellationToken);

            if (!isAvailable)
            {
                _logger.LogWarning("Permission {PermissionId} access denied for tenant {TenantId} - not available in subscription", 
                    permissionId, tenantId);
                return NotFound(); // Return 404 instead of 403 to avoid information disclosure
            }

            return Ok(permission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission {PermissionId} for tenant {TenantId}", permissionId, tenantId);
            return StatusCode(500, "An error occurred while retrieving the permission");
        }
    }

    /// <summary>
    /// Get permission categories available to current tenant
    /// Includes only categories with permissions accessible to tenant
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(PermissionCategory[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionCategory[]>> GetPermissionCategories(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        try
        {
            var categories = await _permissionCatalogService.GetPermissionCategoriesAsync(tenantId, cancellationToken);

            _logger.LogDebug("Retrieved {Count} permission categories for tenant {TenantId}", categories.Length, tenantId);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission categories for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while retrieving permission categories");
        }
    }

    /// <summary>
    /// Search available permissions for current tenant
    /// Results are filtered by tenant's subscription and features
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
            var permissions = await _permissionCatalogService.SearchPermissionsAsync(
                query, tenantId, maxResults, cancellationToken);

            _logger.LogDebug("Tenant search returned {Count} permissions for tenant {TenantId} with query: {Query}", 
                permissions.Length, tenantId, query);

            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching permissions for tenant {TenantId} with query: {Query}", tenantId, query);
            return StatusCode(500, "An error occurred while searching permissions");
        }
    }

    /// <summary>
    /// Check if a permission is available to the current tenant
    /// Useful for UI enablement and role composition validation
    /// </summary>
    [HttpGet("{permissionId}/availability")]
    [ProducesResponseType(typeof(PermissionAvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionAvailabilityResponse>> CheckPermissionAvailability(
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
            var isAvailable = await _permissionCatalogService.IsPermissionAvailableToTenantAsync(
                permissionId, tenantId, cancellationToken);

            string? unavailableReason = null;
            if (!isAvailable)
            {
                // Get permission details to determine why it's unavailable
                var permission = await _permissionCatalogService.GetPermissionByIdAsync(permissionId, cancellationToken);
                if (permission != null)
                {
                    var tenantInfo = await _tenantResolver.GetCurrentTenantInfoAsync(cancellationToken);
                    if (tenantInfo != null)
                    {
                        if (permission.IsDeprecated)
                        {
                            unavailableReason = "Permission is deprecated";
                        }
                        else if (!Enum.TryParse<SubscriptionTier>(tenantInfo.SubscriptionTier, out var currentTier) || 
                                 currentTier < permission.MinimumTier)
                        {
                            unavailableReason = $"Requires {permission.MinimumTier} subscription tier or higher";
                        }
                        else if (permission.RequiredFeatures.Any(f => !tenantInfo.EnabledFeatures.Contains(f)))
                        {
                            var missingFeatures = permission.RequiredFeatures.Except(tenantInfo.EnabledFeatures);
                            unavailableReason = $"Requires features: {string.Join(", ", missingFeatures)}";
                        }
                    }
                }
                else
                {
                    unavailableReason = "Permission does not exist";
                }
            }

            var response = new PermissionAvailabilityResponse
            {
                PermissionId = permissionId,
                IsAvailable = isAvailable,
                UnavailableReason = unavailableReason
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission availability {PermissionId} for tenant {TenantId}", 
                permissionId, tenantId);
            return StatusCode(500, "An error occurred while checking permission availability");
        }
    }

    /// <summary>
    /// Get recommended permissions based on current role permissions
    /// Helps with role composition and discovery
    /// </summary>
    [HttpPost("recommendations")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<string[]>> GetRecommendedPermissions(
        [FromBody] string[] currentPermissions,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantResolver.GetCurrentTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Forbid();
        }

        if (currentPermissions == null)
        {
            return BadRequest("Current permissions array is required");
        }

        try
        {
            var recommendations = await _permissionCatalogService.GetRecommendedPermissionsAsync(
                currentPermissions, tenantId, cancellationToken);

            _logger.LogDebug("Generated {Count} permission recommendations for tenant {TenantId} based on {InputCount} current permissions", 
                recommendations.Length, tenantId, currentPermissions.Length);

            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating permission recommendations for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while generating recommendations");
        }
    }
}

/// <summary>
/// Response model for permission availability checks
/// </summary>
public class PermissionAvailabilityResponse
{
    public string PermissionId { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
}