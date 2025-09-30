using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Service for runtime authorization checks and permission evaluation
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly IUserService _userService;
    private readonly IRuleEngineService _ruleEngineService;
    private readonly IUserRuleService _userRuleService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        IUserService userService,
        IRuleEngineService ruleEngineService,
        IUserRuleService userRuleService,
        ITenantResolver tenantResolver,
        ILogger<AuthorizationService> logger)
    {
        _userService = userService;
        _ruleEngineService = ruleEngineService;
        _userRuleService = userRuleService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        try
        {
            var userPermissions = await _userService.GetUserPermissionsAsync(userId, cancellationToken);
            return userPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", permission, userId);
            return false;
        }
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        var currentUserId = _tenantResolver.GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        return await HasPermissionAsync(currentUserId, permission, cancellationToken);
    }

    public async Task<RuleEvaluationResult> EvaluateRuleAsync(string ruleType, object value, CancellationToken cancellationToken = default)
    {
        var currentUserId = _tenantResolver.GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return RuleEvaluationResult.Error("No user context available");
        }

        return await EvaluateRuleAsync(currentUserId, ruleType, value, cancellationToken);
    }

    public async Task<RuleEvaluationResult> EvaluateRuleAsync(string userId, string ruleType, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve tenant id from resolver if available
            Guid? tenantId = null;
            var tenantIdStr = _tenantResolver.GetCurrentTenantId();
            if (!string.IsNullOrEmpty(tenantIdStr) && Guid.TryParse(tenantIdStr, out var parsed))
            {
                tenantId = parsed;
            }

            var userRules = await _userRuleService.GetUserRulesAsync(userId, tenantId);
            
            if (!userRules.ContainsKey(ruleType))
            {
                return RuleEvaluationResult.NotApplicable(ruleType);
            }

            var ruleValue = userRules[ruleType];
            return await _ruleEngineService.EvaluateRuleAsync(ruleType, ruleValue, value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleType} for user {UserId}", ruleType, userId);
            return RuleEvaluationResult.Error(ruleType, ex.Message);
        }
    }

    public async Task<string[]> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var permissions = await _userService.GetUserPermissionsAsync(userId, cancellationToken);
            return permissions.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user {UserId}", userId);
            return Array.Empty<string>();
        }
    }

    public async Task<string[]> GetMyPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _tenantResolver.GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Array.Empty<string>();
        }

        return await GetUserPermissionsAsync(currentUserId, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetUserRulesAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userRuleService.GetUserRulesAsync(userId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rules for user {UserId}", userId);
            return new Dictionary<string, string>();
        }
    }

    public async Task<Dictionary<string, string>> GetMyRulesAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = _tenantResolver.GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return new Dictionary<string, string>();
        }

        return await GetUserRulesAsync(currentUserId, cancellationToken);
    }

    public async Task<AuthorizationResult> ValidateActionAsync(string action, object? context = null, CancellationToken cancellationToken = default)
    {
        var currentUserId = _tenantResolver.GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return AuthorizationResult.Denied("No user context available", action);
        }

        return await ValidateActionAsync(currentUserId, action, context, cancellationToken);
    }

    public async Task<AuthorizationResult> ValidateActionAsync(string userId, string action, object? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map actions to required permissions
            var requiredPermission = MapActionToPermission(action);
            if (string.IsNullOrEmpty(requiredPermission))
            {
                return AuthorizationResult.Denied($"Unknown action: {action}", action);
            }

            // Check basic permission
            var hasPermission = await HasPermissionAsync(userId, requiredPermission, cancellationToken);
            if (!hasPermission)
            {
                return AuthorizationResult.Denied($"Missing required permission: {requiredPermission}", action);
            }

            // Evaluate context-specific rules if context is provided
            var ruleResults = new List<RuleEvaluationResult>();
            if (context != null)
            {
                var contextRules = ExtractRulesFromContext(action, context);
                foreach (var rule in contextRules)
                {
                    var ruleResult = await EvaluateRuleAsync(userId, rule.Key, rule.Value, cancellationToken);
                    ruleResults.Add(ruleResult);
                    
                    if (!ruleResult.IsAllowed)
                    {
                        return new AuthorizationResult
                        {
                            IsAuthorized = false,
                            Reason = $"Rule evaluation failed: {ruleResult.Reason}",
                            EvaluatedItem = action,
                            RequiredPermission = requiredPermission,
                            RuleResults = ruleResults.ToArray()
                        };
                    }
                }
            }

            _logger.LogInformation("Action {Action} authorized for user {UserId}", action, userId);

            return new AuthorizationResult
            {
                IsAuthorized = true,
                Reason = "Action authorized",
                EvaluatedItem = action,
                RequiredPermission = requiredPermission,
                RuleResults = ruleResults.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating action {Action} for user {UserId}", action, userId);
            return AuthorizationResult.Denied($"Authorization check failed: {ex.Message}", action);
        }
    }

    public async Task<Dictionary<string, bool>> HasPermissionsAsync(string[] permissions, CancellationToken cancellationToken = default)
    {
        var currentUserId = _tenantResolver.GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return permissions.ToDictionary(p => p, p => false);
        }

        return await HasPermissionsAsync(currentUserId, permissions, cancellationToken);
    }

    public async Task<Dictionary<string, bool>> HasPermissionsAsync(string userId, string[] permissions, CancellationToken cancellationToken = default)
    {
        try
        {
            var userPermissions = await GetUserPermissionsAsync(userId, cancellationToken);
            var userPermissionSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);

            return permissions.ToDictionary(
                p => p, 
                p => userPermissionSet.Contains(p)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking multiple permissions for user {UserId}", userId);
            return permissions.ToDictionary(p => p, p => false);
        }
    }

    /// <summary>
    /// Maps action names to required permissions
    /// </summary>
    private string MapActionToPermission(string action)
    {
        return action.ToLowerInvariant() switch
        {
            // User Management Actions
            "user.create" => "users:create",
            "user.view" => "users:view", 
            "user.edit" => "users:edit",
            "user.delete" => "users:delete",
            "user.assign_role" => "users:assign_roles",
            "user.reset_password" => "users:reset_password",

            // Role Management Actions
            "role.create" => "roles:create",
            "role.view" => "roles:view",
            "role.edit" => "roles:edit", 
            "role.delete" => "roles:delete",
            "role.assign_permissions" => "roles:assign_permissions",

            // Loan Actions (examples for business context)
            "loan.create" => "loans:create",
            "loan.approve" => "loans:approve",
            "loan.review" => "loans:review",
            "loan.disburse" => "loans:disburse",

            // Client Actions
            "client.create" => "clients:create",
            "client.view" => "clients:view",
            "client.edit" => "clients:edit",

            // Financial Actions  
            "payment.process" => "payments:process",
            "report.generate" => "reports:generate",
            "audit.access" => "audit:access",

            // System Actions
            "system.configure" => "system:configure",
            "system.backup" => "system:backup",

            _ => string.Empty
        };
    }

    /// <summary>
    /// Extracts rules to evaluate from action context
    /// </summary>
    private Dictionary<string, object> ExtractRulesFromContext(string action, object context)
    {
        var rules = new Dictionary<string, object>();

        // Convert context to dictionary if it's an anonymous object or similar
        if (context is IDictionary<string, object> contextDict)
        {
            // Extract common rule scenarios based on action type
            switch (action.ToLowerInvariant())
            {
                case "loan.approve":
                    if (contextDict.TryGetValue("amount", out var amount))
                        rules["loan_approval_limit"] = amount;
                    if (contextDict.TryGetValue("riskGrade", out var risk))
                        rules["max_risk_grade"] = risk;
                    break;

                case "loan.create":
                    if (contextDict.TryGetValue("amount", out var loanAmount))
                        rules["max_loan_amount"] = loanAmount;
                    break;

                case "payment.process":
                    if (contextDict.TryGetValue("amount", out var payAmount))
                        rules["daily_transaction_limit"] = payAmount;
                    break;
            }
        }

        return rules;
    }
}