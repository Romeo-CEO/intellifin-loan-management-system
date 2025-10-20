using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

/// <summary>
/// Platform Plane Authorization Controller
/// IntelliFin internal team operations for authorization management across all tenants
/// </summary>
[ApiController]
[Route("api/platform/authorization")]
[Produces("application/json")]
public class PlatformAuthorizationController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<PlatformAuthorizationController> _logger;

    public PlatformAuthorizationController(
        IAuthorizationService authorizationService,
        ITenantResolver tenantResolver,
        ILogger<PlatformAuthorizationController> logger)
    {
        _authorizationService = authorizationService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a specific user has a permission (Platform Plane can check any user)
    /// </summary>
    [HttpGet("users/{userId}/permissions/{permission}/check")]
    public async Task<ActionResult<PlatformPermissionCheckResponse>> CheckUserPermission(string userId, string permission)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var hasPermission = await _authorizationService.HasPermissionAsync(userId, permission);
            
            var response = new PlatformPermissionCheckResponse
            {
                UserId = userId,
                Permission = permission,
                HasPermission = hasPermission,
                CheckedAt = DateTime.UtcNow,
                CheckedBy = _tenantResolver.GetCurrentUserId() ?? "system"
            };

            _logger.LogInformation("Platform checked permission {Permission} for user {UserId}: {Result}", 
                permission, userId, hasPermission);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk permission check for a user (Platform Plane only)
    /// </summary>
    [HttpPost("users/{userId}/permissions/bulk-check")]
    public async Task<ActionResult<BulkPermissionCheckResponse>> BulkCheckUserPermissions(
        string userId, 
        [FromBody] BulkPermissionCheckRequest request)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            if (request.Permissions == null || !request.Permissions.Any())
            {
                return BadRequest(new { message = "Permissions list cannot be empty" });
            }

            var results = await _authorizationService.HasPermissionsAsync(userId, request.Permissions);
            
            var response = new BulkPermissionCheckResponse
            {
                UserId = userId,
                Results = results,
                CheckedAt = DateTime.UtcNow,
                CheckedBy = _tenantResolver.GetCurrentUserId() ?? "system"
            };

            _logger.LogInformation("Platform bulk checked {PermissionCount} permissions for user {UserId}", 
                request.Permissions.Length, userId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk checking permissions for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all permissions for a user (Platform Plane can access any user)
    /// </summary>
    [HttpGet("users/{userId}/permissions")]
    public async Task<ActionResult<UserPermissionsResponse>> GetUserPermissions(string userId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var permissions = await _authorizationService.GetUserPermissionsAsync(userId);
            
            var response = new UserPermissionsResponse
            {
                UserId = userId,
                Permissions = permissions,
                RetrievedAt = DateTime.UtcNow,
                RetrievedBy = _tenantResolver.GetCurrentUserId() ?? "system"
            };

            _logger.LogInformation("Platform retrieved {PermissionCount} permissions for user {UserId}", 
                permissions.Length, userId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all rules for a user (Platform Plane can access any user)
    /// </summary>
    [HttpGet("users/{userId}/rules")]
    public async Task<ActionResult<UserRulesResponse>> GetUserRules(string userId)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var rules = await _authorizationService.GetUserRulesAsync(userId);
            
            var response = new UserRulesResponse
            {
                UserId = userId,
                Rules = rules,
                RetrievedAt = DateTime.UtcNow,
                RetrievedBy = _tenantResolver.GetCurrentUserId() ?? "system"
            };

            _logger.LogInformation("Platform retrieved {RuleCount} rules for user {UserId}", 
                rules.Count, userId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rules for user {UserId}", userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Evaluates a rule for a user (Platform Plane can evaluate for any user)
    /// </summary>
    [HttpPost("users/{userId}/rules/{ruleType}/evaluate")]
    public async Task<ActionResult<RuleEvaluationResponse>> EvaluateUserRule(
        string userId, 
        string ruleType, 
        [FromBody] RuleEvaluationRequest request)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var result = await _authorizationService.EvaluateRuleAsync(userId, ruleType, request.Value);
            
            var response = new RuleEvaluationResponse
            {
                UserId = userId,
                RuleType = ruleType,
                EvaluatedValue = request.Value,
                Result = result,
                EvaluatedAt = DateTime.UtcNow,
                EvaluatedBy = _tenantResolver.GetCurrentUserId() ?? "system"
            };

            _logger.LogInformation("Platform evaluated rule {RuleType} for user {UserId}: {IsAllowed}", 
                ruleType, userId, result.IsAllowed);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleType} for user {UserId}", ruleType, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Validates if a user can perform an action (Platform Plane can validate for any user)
    /// </summary>
    [HttpPost("users/{userId}/actions/{action}/validate")]
    public async Task<ActionResult<ActionValidationResponse>> ValidateUserAction(
        string userId, 
        string action, 
        [FromBody] ActionValidationRequest? request = null)
    {
        try
        {
            if (!_tenantResolver.IsPlatformPlane())
            {
                return Forbid("Platform Plane access required");
            }

            var result = await _authorizationService.ValidateActionAsync(userId, action, request?.Context);
            
            var response = new ActionValidationResponse
            {
                UserId = userId,
                Action = action,
                Context = request?.Context,
                Result = result,
                ValidatedAt = DateTime.UtcNow,
                ValidatedBy = _tenantResolver.GetCurrentUserId() ?? "system"
            };

            _logger.LogInformation("Platform validated action {Action} for user {UserId}: {IsAuthorized}", 
                action, userId, result.IsAuthorized);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating action {Action} for user {UserId}", action, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for bulk permission checking
/// </summary>
public class BulkPermissionCheckRequest
{
    public string[] Permissions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Response model for permission checks
/// </summary>
public class PlatformPermissionCheckResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool HasPermission { get; set; }
    public DateTime CheckedAt { get; set; }
    public string CheckedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response model for bulk permission checks
/// </summary>
public class BulkPermissionCheckResponse
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, bool> Results { get; set; } = new();
    public DateTime CheckedAt { get; set; }
    public string CheckedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response model for user permissions
/// </summary>
public class UserPermissionsResponse
{
    public string UserId { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public DateTime RetrievedAt { get; set; }
    public string RetrievedBy { get; set; } = string.Empty;
}

/// <summary>
/// Response model for user rules
/// </summary>
public class UserRulesResponse
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, string> Rules { get; set; } = new();
    public DateTime RetrievedAt { get; set; }
    public string RetrievedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request model for rule evaluation
/// </summary>
public class RuleEvaluationRequest
{
    public object Value { get; set; } = new();
}

/// <summary>
/// Response model for rule evaluation
/// </summary>
public class RuleEvaluationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public object? EvaluatedValue { get; set; }
    public RuleEvaluationResult Result { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
    public string EvaluatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request model for action validation
/// </summary>
public class ActionValidationRequest
{
    public object? Context { get; set; }
}

/// <summary>
/// Response model for action validation
/// </summary>
public class ActionValidationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public object? Context { get; set; }
    public AuthorizationResult Result { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
    public string ValidatedBy { get; set; } = string.Empty;
}