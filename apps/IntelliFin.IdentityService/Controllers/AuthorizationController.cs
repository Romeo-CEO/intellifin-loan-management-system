using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Tenant Plane Authorization Controller
/// Customer organization operations for authorization within their tenant context
/// </summary>
[ApiController]
[Route("api/authorization")]
[Produces("application/json")]
public class AuthorizationController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        IAuthorizationService authorizationService,
        ITenantResolver tenantResolver,
        ILogger<AuthorizationController> logger)
    {
        _authorizationService = authorizationService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current user has a specific permission
    /// </summary>
    [HttpGet("permissions/{permission}/check")]
    public async Task<ActionResult<PermissionCheckResponse>> CheckMyPermission(string permission)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var hasPermission = await _authorizationService.HasPermissionAsync(permission);
            
            var response = new PermissionCheckResponse
            {
                UserId = currentUserId,
                Permission = permission,
                HasPermission = hasPermission,
                CheckedAt = DateTime.UtcNow,
                CheckedBy = currentUserId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for current user", permission);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk permission check for the current user
    /// </summary>
    [HttpPost("permissions/bulk-check")]
    public async Task<ActionResult<BulkPermissionCheckResponse>> BulkCheckMyPermissions(
        [FromBody] BulkPermissionCheckRequest request)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            if (request.Permissions == null || !request.Permissions.Any())
            {
                return BadRequest(new { message = "Permissions list cannot be empty" });
            }

            var results = await _authorizationService.HasPermissionsAsync(request.Permissions);
            
            var response = new BulkPermissionCheckResponse
            {
                UserId = currentUserId,
                Results = results,
                CheckedAt = DateTime.UtcNow,
                CheckedBy = currentUserId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk checking permissions for current user");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all permissions for the current user
    /// </summary>
    [HttpGet("permissions")]
    public async Task<ActionResult<UserPermissionsResponse>> GetMyPermissions()
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var permissions = await _authorizationService.GetMyPermissionsAsync();
            
            var response = new UserPermissionsResponse
            {
                UserId = currentUserId,
                Permissions = permissions,
                RetrievedAt = DateTime.UtcNow,
                RetrievedBy = currentUserId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for current user");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all rules for the current user
    /// </summary>
    [HttpGet("rules")]
    public async Task<ActionResult<UserRulesResponse>> GetMyRules()
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var rules = await _authorizationService.GetMyRulesAsync();
            
            var response = new UserRulesResponse
            {
                UserId = currentUserId,
                Rules = rules,
                RetrievedAt = DateTime.UtcNow,
                RetrievedBy = currentUserId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rules for current user");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Evaluates a rule for the current user
    /// </summary>
    [HttpPost("rules/{ruleType}/evaluate")]
    public async Task<ActionResult<RuleEvaluationResponse>> EvaluateMyRule(
        string ruleType, 
        [FromBody] RuleEvaluationRequest request)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var result = await _authorizationService.EvaluateRuleAsync(ruleType, request.Value);
            
            var response = new RuleEvaluationResponse
            {
                UserId = currentUserId,
                RuleType = ruleType,
                EvaluatedValue = request.Value,
                Result = result,
                EvaluatedAt = DateTime.UtcNow,
                EvaluatedBy = currentUserId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleType} for current user", ruleType);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Validates if the current user can perform an action
    /// </summary>
    [HttpPost("actions/{action}/validate")]
    public async Task<ActionResult<ActionValidationResponse>> ValidateMyAction(
        string action, 
        [FromBody] ActionValidationRequest? request = null)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            var authResult = await _authorizationService.ValidateActionAsync(action, request?.Context);
            var actionResult = MapAuthorizationResult(authResult);
            var response = new ActionValidationResponse
            {
                UserId = currentUserId,
                Action = action,
                Context = request?.Context ?? new Dictionary<string, object>(),
                Result = actionResult,
                ValidatedAt = DateTime.UtcNow,
                ValidatedBy = currentUserId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating action {Action} for current user", action);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Checks if a tenant user has a permission (requires admin permission)
    /// </summary>
    [HttpGet("users/{userId}/permissions/{permission}/check")]
    public async Task<ActionResult<PermissionCheckResponse>> CheckUserPermission(string userId, string permission)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // TODO: Add permission check for user management
            // TODO: Verify target user belongs to current tenant

            var hasPermission = await _authorizationService.HasPermissionAsync(userId, permission);
            
            var response = new PermissionCheckResponse
            {
                UserId = userId,
                Permission = permission,
                HasPermission = hasPermission,
                CheckedAt = DateTime.UtcNow,
                CheckedBy = currentUserId
            };

            _logger.LogInformation("User {CurrentUserId} checked permission {Permission} for user {UserId} in tenant {TenantId}: {Result}", 
                currentUserId, permission, userId, tenantId, hasPermission);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Validates if a tenant user can perform an action (requires admin permission)
    /// </summary>
    [HttpPost("users/{userId}/actions/{action}/validate")]
    public async Task<ActionResult<ActionValidationResponse>> ValidateUserAction(
        string userId, 
        string action, 
        [FromBody] ActionValidationRequest? request = null)
    {
        try
        {
            var tenantId = _tenantResolver.GetCurrentTenantId();
            if (string.IsNullOrEmpty(tenantId))
            {
                return Forbid("Tenant context required");
            }

            var currentUserId = _tenantResolver.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized();
            }

            // TODO: Add permission check for user management  
            // TODO: Verify target user belongs to current tenant

            var result = await _authorizationService.ValidateActionAsync(userId, action, request?.Context);
            
            var response = new ActionValidationResponse
            {
                UserId = userId,
                Action = action,
                Context = request?.Context,
                Result = MapAuthorizationResult(result),
                ValidatedAt = DateTime.UtcNow,
                ValidatedBy = currentUserId
            };

            _logger.LogInformation("User {CurrentUserId} validated action {Action} for user {UserId} in tenant {TenantId}: {IsAuthorized}", 
                currentUserId, action, userId, tenantId, result.IsAuthorized);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating action {Action} for user {UserId}", action, userId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    private ActionValidationResult MapAuthorizationResult(AuthorizationResult authResult)
    {
        if (authResult == null) return new ActionValidationResult { IsAuthorized = false, Reason = "No result" };

        var result = new ActionValidationResult
        {
            IsAuthorized = authResult.IsAuthorized,
            Reason = authResult.Reason,
            EvaluatedItem = authResult.EvaluatedItem,
            RequiredPermission = authResult.RequiredPermission,
            RuleResults = authResult.RuleResults ?? Array.Empty<RuleEvaluationResult>()
        };

        if (authResult.RuleResults != null)
        {
            foreach (var r in authResult.RuleResults)
            {
                if (!r.IsAllowed)
                {
                    result.FailedRules[r.RuleType] = r.Reason;
                }
            }
        }

        return result;
    }
}