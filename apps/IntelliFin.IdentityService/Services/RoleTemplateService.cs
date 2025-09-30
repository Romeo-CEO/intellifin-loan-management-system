using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Constants;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Implementation of role template management service
/// Handles platform-provided role templates for tenant guidance
/// </summary>
public class RoleTemplateService : IRoleTemplateService
{
    private readonly IPermissionCatalogService _permissionCatalogService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RoleTemplateService> _logger;

    // In a real implementation, this would be backed by a database
    // For now, we'll use in-memory storage with predefined templates
    private readonly List<RoleTemplate> _templates;
    private readonly Dictionary<string, RoleTemplateUsageStats> _usageStats;

    public RoleTemplateService(
        IPermissionCatalogService permissionCatalogService,
        IMemoryCache cache,
        ILogger<RoleTemplateService> logger)
    {
        _permissionCatalogService = permissionCatalogService;
        _cache = cache;
        _logger = logger;
        _templates = InitializeDefaultTemplates();
        _usageStats = new Dictionary<string, RoleTemplateUsageStats>();
    }

    public async Task<RoleTemplate> CreateTemplateAsync(
        RoleTemplate template,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new ArgumentException("Template name is required", nameof(template));
        }

        if (_templates.Any(t => t.Name.Equals(template.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Template '{template.Name}' already exists");
        }

        // Validate permissions exist
        await ValidateTemplatePermissionsAsync(template, cancellationToken);

        template.Id = Guid.NewGuid().ToString();
        template.CreatedAt = DateTime.UtcNow;
        template.CreatedBy = createdBy;
        template.IsActive = true;
        template.UsageStats = new RoleTemplateUsageStats();

        _templates.Add(template);
        _usageStats[template.Id] = template.UsageStats;

        _logger.LogInformation("Created role template {TemplateName} ({TemplateId}) by user {UserId}", 
            template.Name, template.Id, createdBy);

        ClearCache();
        return template;
    }

    public async Task<RoleTemplate> UpdateTemplateAsync(
        string templateId,
        RoleTemplate template,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var existingTemplate = _templates.FirstOrDefault(t => t.Id == templateId);
        if (existingTemplate == null)
        {
            throw new KeyNotFoundException($"Template {templateId} not found");
        }

        // Validate permissions exist
        await ValidateTemplatePermissionsAsync(template, cancellationToken);

        // Update properties
        existingTemplate.Name = template.Name;
        existingTemplate.Description = template.Description;
        existingTemplate.Category = template.Category;
        existingTemplate.RecommendedPermissions = template.RecommendedPermissions;
        existingTemplate.RequiredPermissions = template.RequiredPermissions;
        existingTemplate.ProhibitedPermissions = template.ProhibitedPermissions;
        existingTemplate.MinimumTier = template.MinimumTier;
        existingTemplate.RequiredFeatures = template.RequiredFeatures;
        existingTemplate.ApplicableFrameworks = template.ApplicableFrameworks;
        existingTemplate.IsActive = template.IsActive;
        existingTemplate.ComplianceNotes = template.ComplianceNotes;
        existingTemplate.Tags = template.Tags;
        existingTemplate.UpdatedAt = DateTime.UtcNow;
        existingTemplate.UpdatedBy = updatedBy;

        // Increment version
        var versionParts = existingTemplate.Version.Split('.');
        if (versionParts.Length >= 2 && int.TryParse(versionParts[1], out var minorVersion))
        {
            existingTemplate.Version = $"{versionParts[0]}.{minorVersion + 1}";
        }

        _logger.LogInformation("Updated role template {TemplateName} ({TemplateId}) by user {UserId}", 
            existingTemplate.Name, templateId, updatedBy);

        ClearCache();
        return existingTemplate;
    }

    public async Task<bool> DeleteTemplateAsync(
        string templateId,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        var template = _templates.FirstOrDefault(t => t.Id == templateId);
        if (template == null)
        {
            return false;
        }

        // Soft delete - mark as inactive instead of removing
        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = deletedBy;

        _logger.LogInformation("Deleted role template {TemplateName} ({TemplateId}) by user {UserId}", 
            template.Name, templateId, deletedBy);

        ClearCache();
        return true;
    }

    public async Task<RoleTemplate[]> GetAllTemplatesAsync(
        RoleCategory? category = null,
        SubscriptionTier? minimumTier = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"all_templates:{category}:{minimumTier}:{includeInactive}";
        if (_cache.TryGetValue(cacheKey, out RoleTemplate[]? cachedTemplates))
        {
            return cachedTemplates!;
        }

        var query = _templates.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        if (category.HasValue)
        {
            query = query.Where(t => t.Category == category.Value);
        }

        if (minimumTier.HasValue)
        {
            query = query.Where(t => t.MinimumTier <= minimumTier.Value);
        }

        var templates = query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToArray();

        // Cache for 10 minutes
        _cache.Set(cacheKey, templates, TimeSpan.FromMinutes(10));

        return templates;
    }

    public async Task<RoleTemplate[]> GetTemplatesForTenantAsync(
        string tenantId,
        RoleCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        // Get tenant subscription tier and features
        var tenantInfo = await GetTenantInfoAsync(tenantId, cancellationToken);
        if (tenantInfo == null)
        {
            return Array.Empty<RoleTemplate>();
        }

        if (!Enum.TryParse<SubscriptionTier>(tenantInfo.SubscriptionTier, out var tenantTier))
        {
            tenantTier = SubscriptionTier.Starter;
        }

        var allTemplates = await GetAllTemplatesAsync(category, tenantTier, false, cancellationToken);

        // Filter by required features
        var availableTemplates = allTemplates.Where(t =>
            t.RequiredFeatures.All(f => tenantInfo.EnabledFeatures.Contains(f))
        ).ToArray();

        return availableTemplates;
    }

    public async Task<RoleTemplate?> GetTemplateByIdAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        return _templates.FirstOrDefault(t => t.Id == templateId);
    }

    public async Task<RoleTemplate[]> SearchTemplatesAsync(
        string query,
        RoleCategory? category = null,
        SubscriptionTier? minimumTier = null,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var allTemplates = await GetAllTemplatesAsync(category, minimumTier, false, cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
        {
            return allTemplates.Take(maxResults).ToArray();
        }

        var searchResults = allTemplates.Where(t =>
            t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
        ).Take(maxResults).ToArray();

        return searchResults;
    }

    public async Task<RoleTemplate[]> GetTemplatesByCategoryAsync(
        RoleCategory category,
        SubscriptionTier? minimumTier = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await GetAllTemplatesAsync(category, minimumTier, includeInactive, cancellationToken);
    }

    public async Task<RoleTemplateCategorySummary[]> GetTemplateCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheKey = "template_categories";
        if (_cache.TryGetValue(cacheKey, out RoleTemplateCategorySummary[]? cachedCategories))
        {
            return cachedCategories!;
        }

        var activeTemplates = await GetAllTemplatesAsync(includeInactive: false, cancellationToken: cancellationToken);
        var categoryGroups = activeTemplates.GroupBy(t => t.Category);

        var categories = categoryGroups.Select(g => new RoleTemplateCategorySummary
        {
            Category = g.Key,
            TemplateCount = g.Count(),
            Description = GetCategoryDescription(g.Key),
            MostPopularTemplate = GetMostPopularTemplate(g.ToArray()),
            AverageUsage = g.Average(t => t.UsageStats.TenantsUsing)
        }).OrderBy(c => c.Category).ToArray();

        // Cache for 15 minutes
        _cache.Set(cacheKey, categories, TimeSpan.FromMinutes(15));

        return categories;
    }

    public async Task UpdateTemplateUsageAsync(
        string templateId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var template = _templates.FirstOrDefault(t => t.Id == templateId);
        if (template == null)
        {
            return;
        }

        if (!_usageStats.ContainsKey(templateId))
        {
            _usageStats[templateId] = new RoleTemplateUsageStats();
        }

        var stats = _usageStats[templateId];
        stats.TotalRolesCreated++;
        stats.LastCalculatedAt = DateTime.UtcNow;

        template.UsageStats = stats;

        _logger.LogDebug("Updated usage statistics for template {TemplateId} - tenant {TenantId}", 
            templateId, tenantId);

        ClearCache();
    }

    public async Task<TemplateUsageAnalytics[]> GetTemplateUsageAnalyticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var analytics = _templates.Where(t => t.IsActive).Select(t => new TemplateUsageAnalytics
        {
            TemplateId = t.Id,
            TemplateName = t.Name,
            TotalUsage = t.UsageStats.TotalRolesCreated,
            UniqueTenants = t.UsageStats.TenantsUsing,
            UsageTrend = GenerateUsageTrend(t.Id, fromDate, toDate),
            CommonModifications = GetCommonModifications(t.Id),
            AverageRoleUserCount = t.UsageStats.AverageUserCount,
            Rating = CalculateTemplateRating(t)
        }).ToArray();

        return analytics;
    }

    public async Task<TemplateValidationResult> ValidateTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var template = _templates.FirstOrDefault(t => t.Id == templateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template {templateId} not found");
        }

        var result = new TemplateValidationResult
        {
            TemplateId = templateId,
            IsValid = true,
            Issues = Array.Empty<TemplateValidationIssue>(),
            Warnings = Array.Empty<string>(),
            Recommendations = Array.Empty<string>()
        };

        var issues = new List<TemplateValidationIssue>();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        // Validate all permissions exist in current catalog
        var allPermissions = await _permissionCatalogService.GetAllPermissionsAsync(cancellationToken: cancellationToken);
        var validPermissions = allPermissions.Select(p => p.Id).ToHashSet();

        var allTemplatePermissions = template.RecommendedPermissions
            .Concat(template.RequiredPermissions)
            .Concat(template.ProhibitedPermissions)
            .Distinct();

        foreach (var permission in allTemplatePermissions)
        {
            if (!validPermissions.Contains(permission))
            {
                issues.Add(new TemplateValidationIssue
                {
                    Severity = IssueSeverity.High,
                    IssueType = "invalid_permission",
                    Description = $"Permission '{permission}' does not exist in current catalog",
                    Permission = permission,
                    RecommendedAction = "Remove invalid permission or update permission catalog"
                });
            }
        }

        // Check for conflicts between required and prohibited permissions
        var conflictingPermissions = template.RequiredPermissions.Intersect(template.ProhibitedPermissions);
        foreach (var permission in conflictingPermissions)
        {
            issues.Add(new TemplateValidationIssue
            {
                Severity = IssueSeverity.Critical,
                IssueType = "permission_conflict",
                Description = $"Permission '{permission}' is both required and prohibited",
                Permission = permission,
                RecommendedAction = "Remove from either required or prohibited list"
            });
        }

        // Validate segregation of duties
        var segregationIssues = ValidateTemplateSegregation(template.RecommendedPermissions.Concat(template.RequiredPermissions).ToArray());
        issues.AddRange(segregationIssues);

        result.IsValid = !issues.Any(i => i.Severity >= IssueSeverity.High);
        result.Issues = issues.ToArray();
        result.Warnings = warnings.ToArray();
        result.Recommendations = recommendations.ToArray();

        return result;
    }

    public async Task<RoleTemplate[]> GetRecommendedTemplatesAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var availableTemplates = await GetTemplatesForTenantAsync(tenantId, cancellationToken: cancellationToken);
        
        // Simple recommendation logic - can be enhanced with ML/analytics
        var recommendations = availableTemplates
            .Where(t => t.UsageStats.TenantsUsing > 5) // Popular templates
            .OrderByDescending(t => t.UsageStats.TenantsUsing)
            .Take(5)
            .ToArray();

        return recommendations;
    }

    public async Task<TemplateMatchAnalysis> AnalyzeTemplateMatchAsync(
        string templateId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateByIdAsync(templateId, cancellationToken);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template {templateId} not found");
        }

        // This would integrate with RoleCompositionService to analyze tenant's existing roles
        // For now, provide a basic analysis structure
        var analysis = new TemplateMatchAnalysis
        {
            TemplateId = templateId,
            TenantId = tenantId,
            MatchScore = 75, // Would be calculated based on actual role analysis
            HasSimilarRoles = false,
            SimilarRoles = Array.Empty<SimilarRole>(),
            NewPermissions = template.RecommendedPermissions.Take(3).ToArray(),
            MissingPermissions = Array.Empty<string>(),
            BenefitAssessment = new TemplateBenefitAssessment
            {
                ImprovesCompliance = true,
                ReducesComplexity = true,
                ProvidesMissingCapabilities = false,
                ComplianceImprovement = 15,
                Benefits = new[] { "Provides BoZ-compliant role structure", "Reduces setup time" }
            },
            PotentialIssues = Array.Empty<string>()
        };

        return analysis;
    }

    #region Private Helper Methods

    private List<RoleTemplate> InitializeDefaultTemplates()
    {
        return new List<RoleTemplate>
        {
            new RoleTemplate
            {
                Id = "loan-officer-standard",
                Name = "Loan Officer (Standard)",
                Description = "Standard loan officer role for most MFIs",
                Category = RoleCategory.LoanOfficers,
                MinimumTier = SubscriptionTier.Starter,
                RecommendedPermissions = new[] { "clients:view", "clients:create", "clients:edit", "loans:create", "loan_applications:process" },
                RequiredPermissions = new[] { "audit_trail:create" },
                ProhibitedPermissions = new[] { "loans:approve", "loans:disburse" },
                ApplicableFrameworks = new[] { ComplianceFramework.BoZ, ComplianceFramework.AML },
                IsActive = true,
                Version = "1.0",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                CreatedBy = "system",
                UsageStats = new RoleTemplateUsageStats { TenantsUsing = 12, TotalRolesCreated = 48, AverageUserCount = 8 },
                Tags = new[] { "basic", "frontend", "loan-processing" }
            },
            new RoleTemplate
            {
                Id = "head-of-credit",
                Name = "Head of Credit",
                Description = "Senior credit management role with approval authority",
                Category = RoleCategory.CreditManagement,
                MinimumTier = SubscriptionTier.Professional,
                RecommendedPermissions = new[] { "loans:approve", "credit_reports:view", "risk_assessment:perform", "portfolio:analyze" },
                RequiredPermissions = new[] { "audit_trail:create", "compliance:view" },
                ProhibitedPermissions = new[] { "loans:disburse", "gl:post" },
                ApplicableFrameworks = new[] { ComplianceFramework.BoZ, ComplianceFramework.CreditRisk },
                IsActive = true,
                Version = "1.0",
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                CreatedBy = "system",
                UsageStats = new RoleTemplateUsageStats { TenantsUsing = 8, TotalRolesCreated = 16, AverageUserCount = 2 },
                ComplianceNotes = "Must maintain segregation from disbursement functions",
                Tags = new[] { "senior", "credit", "approval" }
            },
            new RoleTemplate
            {
                Id = "branch-manager",
                Name = "Branch Manager",
                Description = "Comprehensive branch management role",
                Category = RoleCategory.Management,
                MinimumTier = SubscriptionTier.Professional,
                RecommendedPermissions = new[] { "branch:manage", "reports:view", "users:view", "loans:view", "clients:view" },
                RequiredPermissions = new[] { "audit_trail:view", "compliance:view" },
                ProhibitedPermissions = new[] { "system:config_edit", "platform:emergency_access" },
                ApplicableFrameworks = new[] { ComplianceFramework.BoZ, ComplianceFramework.InternalAudit },
                IsActive = true,
                Version = "1.0",
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                CreatedBy = "system",
                UsageStats = new RoleTemplateUsageStats { TenantsUsing = 15, TotalRolesCreated = 30, AverageUserCount = 3 },
                Tags = new[] { "management", "oversight", "branch" }
            },
            new RoleTemplate
            {
                Id = "compliance-officer",
                Name = "Compliance Officer",
                Description = "BoZ compliance and risk management specialist",
                Category = RoleCategory.Compliance,
                MinimumTier = SubscriptionTier.Professional,
                RecommendedPermissions = new[] { "compliance:manage", "audit_trail:view", "reports:boz", "risk_assessment:perform" },
                RequiredPermissions = new[] { "compliance:view", "audit_trail:advanced" },
                ProhibitedPermissions = new[] { "loans:approve", "loans:disburse", "gl:post" },
                ApplicableFrameworks = new[] { ComplianceFramework.BoZ, ComplianceFramework.AML, ComplianceFramework.KYC },
                IsActive = true,
                Version = "1.0",
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                CreatedBy = "system",
                UsageStats = new RoleTemplateUsageStats { TenantsUsing = 10, TotalRolesCreated = 12, AverageUserCount = 1 },
                ComplianceNotes = "Essential for BoZ regulatory compliance",
                Tags = new[] { "compliance", "regulatory", "boz" }
            }
        };
    }

    private async Task ValidateTemplatePermissionsAsync(RoleTemplate template, CancellationToken cancellationToken)
    {
        var allPermissions = await _permissionCatalogService.GetAllPermissionsAsync(cancellationToken: cancellationToken);
        var validPermissions = allPermissions.Select(p => p.Id).ToHashSet();

        var allTemplatePermissions = template.RecommendedPermissions
            .Concat(template.RequiredPermissions)
            .Concat(template.ProhibitedPermissions)
            .Distinct();

        var invalidPermissions = allTemplatePermissions.Where(p => !validPermissions.Contains(p)).ToArray();
        if (invalidPermissions.Length > 0)
        {
            throw new ArgumentException($"Invalid permissions: {string.Join(", ", invalidPermissions)}");
        }
    }

    private async Task<TenantInfo?> GetTenantInfoAsync(string tenantId, CancellationToken cancellationToken)
    {
        // This would typically integrate with a tenant service
        // For now, return a basic structure
        return new TenantInfo
        {
            Id = tenantId,
            Name = $"Tenant {tenantId}",
            SubscriptionTier = SubscriptionTier.Professional.ToString(),
            IsActive = true,
            EnabledFeatures = new[] { "loan_origination", "client_management", "basic_reporting" }
        };
    }

    private void ClearCache()
    {
        // In a real implementation, you'd use cache invalidation patterns
        // For now, we'll rely on cache expiration
    }

    private string GetCategoryDescription(RoleCategory category)
    {
        return category switch
        {
            RoleCategory.LoanOfficers => "Front-line loan processing staff",
            RoleCategory.CreditManagement => "Credit analysis and approval roles",
            RoleCategory.Finance => "Financial operations and accounting",
            RoleCategory.Operations => "General operational support",
            RoleCategory.Compliance => "Compliance and risk management",
            RoleCategory.Management => "Management and supervisory roles",
            RoleCategory.Administration => "System and tenant administration",
            _ => "General role category"
        };
    }

    private string? GetMostPopularTemplate(RoleTemplate[] templates)
    {
        return templates.OrderByDescending(t => t.UsageStats.TenantsUsing).FirstOrDefault()?.Name;
    }

    private TemplateUsageTrend[] GenerateUsageTrend(string templateId, DateTime? fromDate, DateTime? toDate)
    {
        // In a real implementation, this would query usage history
        // For now, generate sample data
        var trends = new List<TemplateUsageTrend>();
        var startDate = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var endDate = toDate ?? DateTime.UtcNow;

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            trends.Add(new TemplateUsageTrend
            {
                Date = date,
                RolesCreated = Random.Shared.Next(0, 3),
                UniqueTenants = Random.Shared.Next(0, 2)
            });
        }

        return trends.ToArray();
    }

    private TemplateModificationStats[] GetCommonModifications(string templateId)
    {
        // In a real implementation, this would analyze modification patterns
        return new[]
        {
            new TemplateModificationStats
            {
                ModificationType = "permission_added",
                Permission = "reports:export",
                TenantCount = 5,
                Percentage = 42.0
            },
            new TemplateModificationStats
            {
                ModificationType = "permission_removed",
                Permission = "audit_trail:create",
                TenantCount = 2,
                Percentage = 17.0
            }
        };
    }

    private double CalculateTemplateRating(RoleTemplate template)
    {
        // Simple rating calculation based on usage
        var usageScore = Math.Min(5.0, template.UsageStats.TenantsUsing / 10.0 * 5.0);
        var ageScore = Math.Max(1.0, 5.0 - ((DateTime.UtcNow - template.CreatedAt).TotalDays / 365.0));
        
        return Math.Round((usageScore + ageScore) / 2.0, 1);
    }

    private TemplateValidationIssue[] ValidateTemplateSegregation(string[] permissions)
    {
        var issues = new List<TemplateValidationIssue>();

        // Define segregation rules
        var segregationRules = new Dictionary<string, string[]>
        {
            ["loans:approve"] = new[] { "loans:disburse", "gl:post" },
            ["loans:disburse"] = new[] { "loans:approve", "payments:record" },
            ["gl:post"] = new[] { "gl:reverse", "loans:approve" }
        };

        foreach (var permission in permissions)
        {
            if (segregationRules.TryGetValue(permission, out var conflictingPermissions))
            {
                var conflicts = conflictingPermissions.Intersect(permissions);
                foreach (var conflict in conflicts)
                {
                    issues.Add(new TemplateValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        IssueType = "segregation_conflict",
                        Description = $"Permission '{permission}' conflicts with '{conflict}' (segregation of duties)",
                        Permission = permission,
                        RecommendedAction = $"Remove either '{permission}' or '{conflict}' from template"
                    });
                }
            }
        }

        return issues.ToArray();
    }

    #endregion
}