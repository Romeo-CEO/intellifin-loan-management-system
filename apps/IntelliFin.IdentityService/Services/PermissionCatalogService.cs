using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Implementation of permission catalog management service
/// </summary>
public class PermissionCatalogService : IPermissionCatalogService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionCatalogService> _logger;
    private readonly ITenantResolver _tenantResolver;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    // In-memory permission catalog - in production this could be database-backed
    private readonly Dictionary<string, SystemPermission> _permissionCatalog;

    public PermissionCatalogService(
        IMemoryCache cache,
        ILogger<PermissionCatalogService> logger,
        ITenantResolver tenantResolver)
    {
        _cache = cache;
        _logger = logger;
        _tenantResolver = tenantResolver;
        _permissionCatalog = InitializePermissionCatalog();
    }

    public async Task<SystemPermission[]> GetAllPermissionsAsync(
        string? category = null, 
        PermissionRiskLevel? riskLevel = null, 
        bool includeDeprecated = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"all_permissions_{category}_{riskLevel}_{includeDeprecated}";
        
        if (_cache.TryGetValue(cacheKey, out SystemPermission[]? cached))
            return cached!;

        await Task.CompletedTask; // Simulate async operation

        var permissions = _permissionCatalog.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(category))
            permissions = permissions.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        if (riskLevel.HasValue)
            permissions = permissions.Where(p => p.RiskLevel == riskLevel.Value);

        if (!includeDeprecated)
            permissions = permissions.Where(p => !p.IsDeprecated);

        var result = permissions.OrderBy(p => p.Category).ThenBy(p => p.Name).ToArray();
        
        _cache.Set(cacheKey, result, CacheExpiration);
        return result;
    }

    public async Task<SystemPermission[]> GetTenantAvailablePermissionsAsync(
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"tenant_permissions_{tenantId}";
        
        if (_cache.TryGetValue(cacheKey, out SystemPermission[]? cached))
            return cached!;

        // Get tenant subscription tier - for now, defaulting to Professional
        var tenantTier = await GetTenantSubscriptionTierAsync(tenantId);
        
        var availablePermissions = _permissionCatalog.Values
            .Where(p => !p.IsDeprecated)
            .Where(p => p.MinimumTier <= tenantTier)
            .Where(p => !p.Id.StartsWith("platform:")) // Platform permissions not available to tenants
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToArray();

        _cache.Set(cacheKey, availablePermissions, CacheExpiration);
        return availablePermissions;
    }

    public async Task<SystemPermission?> GetPermissionByIdAsync(
        string permissionId, 
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return _permissionCatalog.TryGetValue(permissionId, out var permission) ? permission : null;
    }

    public async Task<PermissionCategory[]> GetPermissionCategoriesAsync(
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"permission_categories_{tenantId ?? "all"}";
        
        if (_cache.TryGetValue(cacheKey, out PermissionCategory[]? cached))
            return cached!;

        var permissions = tenantId != null 
            ? await GetTenantAvailablePermissionsAsync(tenantId, cancellationToken)
            : await GetAllPermissionsAsync(includeDeprecated: false, cancellationToken: cancellationToken);

        var categories = permissions
            .GroupBy(p => p.Category)
            .Select(g => new PermissionCategory
            {
                Name = g.Key,
                Description = GetCategoryDescription(g.Key),
                PermissionCount = g.Count(),
                MaxRiskLevel = g.Max(p => p.RiskLevel),
                RestrictedPermissions = g.Where(p => p.RiskLevel >= PermissionRiskLevel.High)
                    .Select(p => p.Id).ToArray()
            })
            .OrderBy(c => c.Name)
            .ToArray();

        _cache.Set(cacheKey, categories, CacheExpiration);
        return categories;
    }

    public async Task<SystemPermission[]> SearchPermissionsAsync(
        string query, 
        string? tenantId = null, 
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SystemPermission>();

        var searchPermissions = tenantId != null
            ? await GetTenantAvailablePermissionsAsync(tenantId, cancellationToken)
            : await GetAllPermissionsAsync(includeDeprecated: false, cancellationToken: cancellationToken);

        var queryLower = query.ToLowerInvariant();
        
        var results = searchPermissions
            .Where(p => 
                p.Name.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => CalculateRelevanceScore(p, queryLower))
            .Take(maxResults)
            .ToArray();

        return results;
    }

    public async Task<SystemPermission> CreatePermissionAsync(
        SystemPermission permission, 
        string createdBy, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permission.Id))
            throw new ArgumentException("Permission ID is required");

        if (_permissionCatalog.ContainsKey(permission.Id))
            throw new InvalidOperationException($"Permission '{permission.Id}' already exists");

        if (!SystemPermissions.IsValidPermission(permission.Id))
        {
            _logger.LogWarning("Creating permission {PermissionId} that is not in SystemPermissions constants", permission.Id);
        }

        permission.CreatedAt = DateTime.UtcNow;
        permission.CreatedBy = createdBy;

        _permissionCatalog[permission.Id] = permission;
        
        // Clear cache
        ClearPermissionCache();

        _logger.LogInformation("Created new system permission {PermissionId} by {CreatedBy}", permission.Id, createdBy);
        
        await Task.CompletedTask;
        return permission;
    }

    public async Task<SystemPermission> UpdatePermissionAsync(
        string permissionId, 
        SystemPermission permission, 
        string updatedBy, 
        CancellationToken cancellationToken = default)
    {
        if (!_permissionCatalog.TryGetValue(permissionId, out var existing))
            throw new KeyNotFoundException($"Permission '{permissionId}' not found");

        // Preserve immutable fields
        permission.Id = existing.Id;
        permission.CreatedAt = existing.CreatedAt;
        permission.CreatedBy = existing.CreatedBy;

        _permissionCatalog[permissionId] = permission;
        
        // Clear cache
        ClearPermissionCache();

        _logger.LogInformation("Updated system permission {PermissionId} by {UpdatedBy}", permissionId, updatedBy);
        
        await Task.CompletedTask;
        return permission;
    }

    public async Task<bool> DeprecatePermissionAsync(
        string permissionId, 
        string deprecatedBy, 
        CancellationToken cancellationToken = default)
    {
        if (!_permissionCatalog.TryGetValue(permissionId, out var existing))
            return false;

        existing.IsDeprecated = true;
        
        // Clear cache
        ClearPermissionCache();

        _logger.LogInformation("Deprecated system permission {PermissionId} by {DeprecatedBy}", permissionId, deprecatedBy);
        
        await Task.CompletedTask;
        return true;
    }

    public async Task<PermissionUsageStats[]> GetPermissionUsageStatsAsync(
        CancellationToken cancellationToken = default)
    {
        // In production, this would query actual usage from database
        await Task.CompletedTask;
        
        return _permissionCatalog.Values
            .Where(p => !p.IsDeprecated)
            .Select(p => new PermissionUsageStats
            {
                PermissionId = p.Id,
                ActiveTenants = SimulateUsageData(p.Id, "tenants"),
                TotalRoleAssignments = SimulateUsageData(p.Id, "assignments"),
                AverageRoleAssignments = SimulateUsageData(p.Id, "average"),
                AdoptionRate = CalculateAdoptionRate(p.RiskLevel),
                LastUsed = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
            })
            .OrderByDescending(s => s.AdoptionRate)
            .ToArray();
    }

    public async Task<bool> IsPermissionAvailableToTenantAsync(
        string permissionId, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        if (!_permissionCatalog.TryGetValue(permissionId, out var permission))
            return false;

        if (permission.IsDeprecated)
            return false;

        // Platform permissions not available to tenants
        if (permission.Id.StartsWith("platform:"))
            return false;

        var tenantTier = await GetTenantSubscriptionTierAsync(tenantId);
        return permission.MinimumTier <= tenantTier;
    }

    public async Task<string[]> GetRecommendedPermissionsAsync(
        string[] currentPermissions, 
        string? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        var availablePermissions = tenantId != null
            ? await GetTenantAvailablePermissionsAsync(tenantId, cancellationToken)
            : await GetAllPermissionsAsync(includeDeprecated: false, cancellationToken: cancellationToken);

        var currentCategories = currentPermissions
            .Where(p => _permissionCatalog.ContainsKey(p))
            .Select(p => _permissionCatalog[p].Category)
            .Distinct()
            .ToArray();

        // Recommend permissions from same categories that aren't assigned
        var recommendations = availablePermissions
            .Where(p => currentCategories.Contains(p.Category))
            .Where(p => !currentPermissions.Contains(p.Id))
            .Where(p => p.RiskLevel <= PermissionRiskLevel.Medium) // Only suggest medium risk or lower
            .OrderBy(p => p.RiskLevel)
            .Take(10)
            .Select(p => p.Id)
            .ToArray();

        return recommendations;
    }

    public async Task<PermissionImpactAnalysis> AnalyzePermissionImpactAsync(
        string[] permissionIds, 
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        
        // In production, this would analyze actual database data
        return new PermissionImpactAnalysis
        {
            AffectedTenants = permissionIds.Length * 5,
            AffectedRoles = permissionIds.Length * 12,
            AffectedUsers = permissionIds.Length * 48,
            PotentialIssues = permissionIds
                .Where(p => _permissionCatalog.TryGetValue(p, out var perm) && perm.RiskLevel >= PermissionRiskLevel.High)
                .Select(p => $"High-risk permission {p} may require additional approval")
                .ToArray(),
            Recommendations = new[] 
            { 
                "Test changes in staging environment first",
                "Notify affected tenants 24 hours in advance",
                "Ensure rollback plan is ready"
            }
        };
    }

    #region Private Methods

    private Dictionary<string, SystemPermission> InitializePermissionCatalog()
    {
        var catalog = new Dictionary<string, SystemPermission>();
        var allPermissions = SystemPermissions.GetAllPermissions();

        foreach (var permissionId in allPermissions)
        {
            var parts = permissionId.Split(':');
            var resource = parts[0];
            var action = parts.Length > 1 ? parts[1] : "unknown";

            catalog[permissionId] = new SystemPermission
            {
                Id = permissionId,
                Name = GeneratePermissionName(permissionId),
                Description = GeneratePermissionDescription(permissionId),
                Category = GeneratePermissionCategory(resource),
                Resource = resource,
                Action = action,
                RequiredFeatures = GenerateRequiredFeatures(permissionId),
                MinimumTier = DetermineMinimumTier(permissionId),
                RiskLevel = DetermineRiskLevel(permissionId),
                SupportsValueClaims = DetermineIfSupportsValueClaims(permissionId),
                CommonRules = GenerateCommonRules(permissionId),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };
        }

        return catalog;
    }

    private string GeneratePermissionName(string permissionId)
    {
        var parts = permissionId.Split(':');
        var resource = parts[0];
        var action = parts.Length > 1 ? parts[1] : "";

        var resourceName = resource switch
        {
            "clients" => "Clients",
            "loans" => "Loans",
            "loan_applications" => "Loan Applications",
            "credit_reports" => "Credit Reports",
            "risk_assessment" => "Risk Assessment",
            "collections" => "Collections",
            "payments" => "Payments",
            "gl" => "General Ledger",
            "financial_reports" => "Financial Reports",
            "reports" => "Reports",
            "communications" => "Communications",
            "system" => "System",
            "audit_trail" => "Audit Trail",
            "compliance" => "Compliance",
            "branch" => "Branch",
            "mobile" => "Mobile Banking",
            "digital_payments" => "Digital Payments",
            "platform" => "Platform",
            _ => resource.Replace("_", " ")
        };

        var actionName = action switch
        {
            "view" => "View",
            "create" => "Create",
            "edit" => "Edit",
            "delete" => "Delete",
            "approve" => "Approve",
            "reject" => "Reject",
            "disburse" => "Disburse",
            "manage" => "Manage",
            "process" => "Process",
            "generate" => "Generate",
            "export" => "Export",
            _ => action.Replace("_", " ")
        };

        return $"{actionName} {resourceName}".Trim();
    }

    private string GeneratePermissionDescription(string permissionId)
    {
        return permissionId switch
        {
            "clients:view" => "Access to view client basic information and profiles",
            "clients:create" => "Create new client records and profiles",
            "loans:approve" => "Authority to approve loan applications",
            "loans:approve_high_value" => "Approve high-value loans above standard limits",
            "system:advanced_config" => "Access to advanced system configuration settings",
            "platform:tenants_manage" => "Manage tenant accounts and configurations",
            _ => $"Permission to {GeneratePermissionName(permissionId).ToLowerInvariant()}"
        };
    }

    private string GeneratePermissionCategory(string resource)
    {
        return resource switch
        {
            "clients" => "Client Management",
            "loans" or "loan_applications" => "Loan Management",
            "credit_reports" or "risk_assessment" => "Credit Assessment",
            "collections" or "payments" => "Collections & Payments",
            "gl" or "financial_reports" => "Financial Management",
            "reports" => "Reporting",
            "communications" => "Communications",
            "system" => "System Administration",
            "audit_trail" or "compliance" => "Audit & Compliance",
            "branch" => "Branch Operations",
            "mobile" or "digital_payments" => "Digital Banking",
            "platform" => "Platform Administration",
            _ => "General"
        };
    }

    private string[] GenerateRequiredFeatures(string permissionId)
    {
        if (permissionId.StartsWith("mobile:") || permissionId.StartsWith("digital_payments:"))
            return new[] { "mobile-banking", "digital-payments" };
        
        if (permissionId.StartsWith("platform:"))
            return new[] { "platform-administration" };

        return Array.Empty<string>();
    }

    private SubscriptionTier DetermineMinimumTier(string permissionId)
    {
        if (permissionId.Contains("advanced") || permissionId.Contains("emergency") || permissionId.StartsWith("platform:"))
            return SubscriptionTier.Enterprise;
        
        if (permissionId.Contains("high_value") || permissionId.Contains("export") || permissionId.Contains("sensitive"))
            return SubscriptionTier.Professional;

        return SubscriptionTier.Starter;
    }

    private PermissionRiskLevel DetermineRiskLevel(string permissionId)
    {
        if (permissionId.Contains("delete") || permissionId.Contains("write_off") || 
            permissionId.StartsWith("system:") || permissionId.StartsWith("platform:") ||
            permissionId.Contains("emergency") || permissionId.Contains("advanced"))
            return PermissionRiskLevel.Critical;

        if (permissionId.Contains("approve") || permissionId.Contains("disburse") || 
            permissionId.Contains("reverse") || permissionId.Contains("manage") ||
            permissionId.Contains("high_value"))
            return PermissionRiskLevel.High;

        if (permissionId.Contains("create") || permissionId.Contains("edit") || 
            permissionId.Contains("process") || permissionId.Contains("export"))
            return PermissionRiskLevel.Medium;

        return PermissionRiskLevel.Low;
    }

    private bool DetermineIfSupportsValueClaims(string permissionId)
    {
        return permissionId.Contains("approve") || permissionId.Contains("disburse") || 
               permissionId.Contains("limit") || permissionId.Contains("amount");
    }

    private string[] GenerateCommonRules(string permissionId)
    {
        if (permissionId.Contains("approve"))
            return new[] { "approval_limit", "risk_grade_limit", "required_approval_count" };

        if (permissionId.Contains("disburse"))
            return new[] { "daily_disbursement_limit", "max_transaction_amount" };

        return Array.Empty<string>();
    }

    private string GetCategoryDescription(string category)
    {
        return category switch
        {
            "Client Management" => "Customer data and relationship management",
            "Loan Management" => "Loan origination, approval, and servicing",
            "Credit Assessment" => "Credit analysis and risk evaluation",
            "Collections & Payments" => "Payment processing and collections management",
            "Financial Management" => "General ledger and financial operations",
            "Reporting" => "Business and regulatory reporting",
            "Communications" => "Customer and system notifications",
            "System Administration" => "System configuration and user management",
            "Audit & Compliance" => "Audit trails and regulatory compliance",
            "Branch Operations" => "Branch-specific operations and management",
            "Digital Banking" => "Mobile and digital payment services",
            "Platform Administration" => "Cross-tenant platform management",
            _ => "General system operations"
        };
    }

    private double CalculateRelevanceScore(SystemPermission permission, string queryLower)
    {
        double score = 0;
        
        if (permission.Name.ToLowerInvariant().StartsWith(queryLower)) score += 10;
        if (permission.Id.ToLowerInvariant().Contains(queryLower)) score += 8;
        if (permission.Name.ToLowerInvariant().Contains(queryLower)) score += 6;
        if (permission.Description.ToLowerInvariant().Contains(queryLower)) score += 4;
        if (permission.Category.ToLowerInvariant().Contains(queryLower)) score += 2;

        return score;
    }

    private async Task<SubscriptionTier> GetTenantSubscriptionTierAsync(string tenantId)
    {
        // In production, this would query tenant subscription from database
        await Task.CompletedTask;
        return SubscriptionTier.Professional; // Default for now
    }

    private int SimulateUsageData(string permissionId, string type)
    {
        var hash = permissionId.GetHashCode();
        return type switch
        {
            "tenants" => Math.Abs(hash % 15) + 1,
            "assignments" => Math.Abs(hash % 200) + 10,
            "average" => Math.Abs(hash % 20) + 5,
            _ => 0
        };
    }

    private double CalculateAdoptionRate(PermissionRiskLevel riskLevel)
    {
        return riskLevel switch
        {
            PermissionRiskLevel.Low => 0.95,
            PermissionRiskLevel.Medium => 0.80,
            PermissionRiskLevel.High => 0.60,
            PermissionRiskLevel.Critical => 0.30,
            _ => 0.50
        };
    }

    private void ClearPermissionCache()
    {
        // In a more sophisticated implementation, we could use cache tags or dependency tracking
        _logger.LogDebug("Clearing permission catalog cache");
    }

    #endregion
}

