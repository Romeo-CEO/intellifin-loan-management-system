using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Platform-level service for managing rule templates and analytics
/// </summary>
public class RuleTemplateService : IRuleTemplateService
{
    private readonly LmsDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RuleTemplateService> _logger;
    
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);
    private const string RULE_TEMPLATES_CACHE_KEY = "all_rule_templates";
    private const string TEMPLATE_ANALYTICS_CACHE_KEY = "template_analytics";
    private const string TENANT_RULE_ANALYSIS_CACHE_KEY = "tenant_rule_analysis_{0}";

    public RuleTemplateService(
        LmsDbContext context,
        IMemoryCache cache,
        ILogger<RuleTemplateService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RuleTemplate> CreateRuleTemplateAsync(CreateRuleTemplateRequest request)
    {
        try
        {
            // Validate the template
            var validation = await ValidateRuleTemplateAsync(request.Template);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid template: {string.Join(", ", validation.Errors)}");
            }

            var template = new RuleTemplate
            {
                Id = request.Template.Id ?? GenerateTemplateId(request.Template.Name),
                Name = request.Template.Name,
                Description = request.Template.Description,
                Category = request.Template.Category,
                ValueType = request.Template.ValueType,
                MinValue = request.Template.MinValue,
                MaxValue = request.Template.MaxValue,
                AllowedValues = request.Template.AllowedValues,
                DefaultValue = request.Template.DefaultValue,
                ValidationLogic = request.Template.ValidationLogic,
                RequiresCompliance = request.Template.RequiresCompliance,
                MinimumTier = request.Template.MinimumTier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                Version = 1
            };

            // Save to storage (in a real implementation, this would be a database table)
            await SaveRuleTemplateAsync(template);

            // Invalidate cache
            _cache.Remove(RULE_TEMPLATES_CACHE_KEY);
            _cache.Remove(TEMPLATE_ANALYTICS_CACHE_KEY);

            _logger.LogInformation("Created rule template {TemplateId} by {CreatedBy}", template.Id, request.CreatedBy);

            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rule template {TemplateName}", request.Template.Name);
            throw;
        }
    }

    public async Task<RuleTemplate> UpdateRuleTemplateAsync(string templateId, UpdateRuleTemplateRequest request)
    {
        try
        {
            var existing = await GetRuleTemplateAsync(templateId);
            if (existing == null)
            {
                throw new InvalidOperationException($"Rule template {templateId} not found");
            }

            // Create updated template
            var updated = new RuleTemplate
            {
                Id = existing.Id,
                Name = request.Name ?? existing.Name,
                Description = request.Description ?? existing.Description,
                Category = request.Category ?? existing.Category,
                ValueType = existing.ValueType,
                MinValue = request.MinValue ?? existing.MinValue,
                MaxValue = request.MaxValue ?? existing.MaxValue,
                DefaultValue = request.DefaultValue ?? existing.DefaultValue,
                AllowedValues = request.AllowedValues ?? existing.AllowedValues,
                ValidationLogic = request.ValidationLogic ?? existing.ValidationLogic,
                RequiresCompliance = request.RequiresCompliance ?? existing.RequiresCompliance,
                MinimumTier = request.MinimumTier ?? existing.MinimumTier,
                RequiredFeatures = existing.RequiredFeatures,
                IsActive = request.IsActive ?? existing.IsActive,
                CreatedAt = existing.CreatedAt,
                CreatedBy = existing.CreatedBy,
                Version = existing.Version + 1,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.UpdatedBy,
                UsageStats = existing.UsageStats,
                RegulatoryMax = existing.RegulatoryMax,
                Currency = existing.Currency,
                RiskLevel = existing.RiskLevel
            };

            // Validate updated template
            var validation = await ValidateRuleTemplateAsync(updated);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid template update: {string.Join(", ", validation.Errors)}");
            }

            await SaveRuleTemplateAsync(updated);

            // Invalidate cache
            _cache.Remove(RULE_TEMPLATES_CACHE_KEY);
            _cache.Remove(TEMPLATE_ANALYTICS_CACHE_KEY);

            _logger.LogInformation("Updated rule template {TemplateId} to version {Version} by {UpdatedBy}", 
                templateId, updated.Version, request.UpdatedBy);

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rule template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<RuleTemplate?> GetRuleTemplateAsync(string templateId)
    {
        try
        {
            // Try to get from system rules first
            if (SystemRules.IsValidRule(templateId))
            {
                return await GenerateSystemRuleTemplateAsync(templateId);
            }

            // Otherwise load from storage
            return await LoadRuleTemplateAsync(templateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rule template {TemplateId}", templateId);
            return null;
        }
    }

    public async Task<RuleTemplateListResponse> GetRuleTemplatesAsync(RuleTemplateFilter? filter = null)
    {
        try
        {
            if (!_cache.TryGetValue(RULE_TEMPLATES_CACHE_KEY, out List<RuleTemplate>? templates))
            {
                templates = await LoadAllRuleTemplatesAsync();
                _cache.Set(RULE_TEMPLATES_CACHE_KEY, templates, CacheExpiry);
            }

            var filteredTemplates = ApplyFilter(templates!, filter);

            return new RuleTemplateListResponse
            {
                Templates = filteredTemplates.ToArray(),
                TotalCount = filteredTemplates.Count,
                Categories = GetCategories(filteredTemplates),
                FilterInfo = filter != null ? $"Filtered by: {GetFilterDescription(filter)}" : "No filters applied"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rule templates with filter {@Filter}", filter);
            throw;
        }
    }

    public async Task<bool> DeleteRuleTemplateAsync(string templateId)
    {
        try
        {
            // Check if template is a system rule (cannot be deleted)
            if (SystemRules.IsValidRule(templateId))
            {
                throw new InvalidOperationException("System rule templates cannot be deleted");
            }

            // Check if template is in use
            var usage = await GetTemplateUsageStatsAsync(templateId);
            if (usage.TenantsUsing > 0)
            {
                throw new InvalidOperationException($"Template is in use by {usage.TenantsUsing} tenant(s)");
            }

            var deleted = await DeleteRuleTemplateFromStorageAsync(templateId);
            
            if (deleted)
            {
                // Invalidate cache
                _cache.Remove(RULE_TEMPLATES_CACHE_KEY);
                _cache.Remove(TEMPLATE_ANALYTICS_CACHE_KEY);

                _logger.LogInformation("Deleted rule template {TemplateId}", templateId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rule template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<RuleTemplateValidationResult> ValidateRuleTemplateAsync(RuleTemplate template)
    {
        var result = new RuleTemplateValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(template.Id))
            {
                result.Errors.Add("Template ID is required");
            }

            if (string.IsNullOrWhiteSpace(template.Name))
            {
                result.Errors.Add("Template name is required");
            }

            if (string.IsNullOrWhiteSpace(template.Description))
            {
                result.Errors.Add("Template description is required");
            }

            // Value type validation
            if (!Enum.IsDefined<RuleValueType>(template.ValueType))
            {
                result.Errors.Add("Invalid value type");
            }

            // Range validation
            if (template.MinValue != null && template.MaxValue != null)
            {
                if (!ValidateValueRange(template.MinValue, template.MaxValue, template.ValueType))
                {
                    result.Errors.Add("Invalid value range: minimum must be less than maximum");
                }
            }

            // Allowed values validation for enum types
            if (template.ValueType == RuleValueType.Enum && (template.AllowedValues?.Length ?? 0) == 0)
            {
                result.Errors.Add("Enum type templates must specify allowed values");
            }

            // Validation logic syntax check
            if (!string.IsNullOrWhiteSpace(template.ValidationLogic))
            {
                var syntaxValidation = await ValidateTemplateLogicAsync(template.Id, template.ValidationLogic);
                if (!syntaxValidation.IsValid)
                {
                    result.Errors.AddRange(syntaxValidation.Errors);
                }
            }

            // Business rule validation
            if (template.RequiresCompliance && template.MinimumTier < SubscriptionTier.Professional)
            {
                result.Warnings.Add("Compliance rules typically require Professional tier or higher");
            }

            result.IsValid = result.Errors.Count == 0;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rule template {TemplateId}", template.Id);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    public async Task<Dictionary<string, RuleTemplate[]>> GetRuleTemplatesByCategoryAsync()
    {
        try
        {
            var templates = await GetRuleTemplatesAsync();
            
            return templates.Templates
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rule templates by category");
            throw;
        }
    }

    public async Task<RuleTemplateUsageStats> GetTemplateUsageStatsAsync(string templateId)
    {
        try
        {
            // In a real implementation, this would query actual usage data
            var stats = new RuleTemplateUsageStats
            {
                TenantsUsing = await GetTenantUsageCountAsync(templateId),
                TotalRoles = await GetRoleUsageCountAsync(templateId),
                TotalRulesCreated = await GetTotalRulesCreatedAsync(templateId),
                AverageValue = (double)(await GetAverageRuleValueAsync(templateId)),
                ComplianceRate = await GetComplianceRateAsync(templateId),
                LastCalculatedAt = DateTimeOffset.UtcNow
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template usage stats for {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<RuleTemplateAnalytics> GetTemplateAnalyticsAsync()
    {
        try
        {
            if (!_cache.TryGetValue(TEMPLATE_ANALYTICS_CACHE_KEY, out RuleTemplateAnalytics? analytics))
            {
                analytics = await GenerateTemplateAnalyticsAsync();
                _cache.Set(TEMPLATE_ANALYTICS_CACHE_KEY, analytics, TimeSpan.FromHours(1));
            }

            return analytics!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template analytics");
            throw;
        }
    }

    public async Task<RuleEngineDeploymentResult> DeployRuleEngineUpdatesAsync(RuleEngineDeploymentRequest request)
    {
        try
        {
            var result = new RuleEngineDeploymentResult
            {
                DeploymentId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                Status = "in_progress",
                AffectedTenants = new List<Guid>()
            };

            _logger.LogInformation("Starting rule engine deployment {DeploymentId}", result.DeploymentId);

            // Get affected tenants
            var tenants = await GetAffectedTenantsAsync(request);
            result.AffectedTenants = tenants;

            // Simulate deployment process
            foreach (var tenantId in tenants)
            {
                try
                {
                    await DeployToTenantAsync(tenantId, request);
                    result.SuccessfulTenants++;
                }
                catch (Exception ex)
                {
                    result.FailedTenants++;
                    result.Errors.Add($"Tenant {tenantId}: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to deploy to tenant {TenantId}", tenantId);
                }
            }

            result.CompletedAt = DateTime.UtcNow;
            result.Status = result.FailedTenants == 0 ? "completed" : "completed_with_errors";

            _logger.LogInformation("Completed rule engine deployment {DeploymentId}: {Success}/{Total} tenants", 
                result.DeploymentId, result.SuccessfulTenants, tenants.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying rule engine updates");
            throw;
        }
    }

    public async Task<TenantRuleAnalysis> AnalyzeTenantRuleUsageAsync(Guid tenantId)
    {
        try
        {
            var cacheKey = string.Format(TENANT_RULE_ANALYSIS_CACHE_KEY, tenantId);
            
            if (!_cache.TryGetValue(cacheKey, out TenantRuleAnalysis? analysis))
            {
                analysis = await GenerateTenantRuleAnalysisAsync(tenantId);
                _cache.Set(cacheKey, analysis, TimeSpan.FromMinutes(30));
            }

            return analysis!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing tenant rule usage for {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<ComplianceScanResult> PerformComplianceScanAsync(ComplianceScanRequest? request = null)
    {
        try
        {
            var result = new ComplianceScanResult
            {
                ScanId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                TotalTenants = 0,
                CompliantTenants = 0,
                ViolatingTenants = 0,
                Violations = new List<ComplianceViolation>()
            };

            _logger.LogInformation("Starting compliance scan {ScanId}", result.ScanId);

            // Get all tenants or filtered set
            var tenants = await GetTenantsForComplianceScanAsync(request);
            result.TotalTenants = tenants.Count;

            foreach (var tenantId in tenants)
            {
                try
                {
                    var tenantCompliance = await CheckTenantComplianceAsync(tenantId);
                    
                    if (tenantCompliance.IsCompliant)
                    {
                        result.CompliantTenants++;
                    }
                    else
                    {
                        result.ViolatingTenants++;
                        result.Violations.AddRange(tenantCompliance.Violations);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to scan tenant {TenantId} for compliance", tenantId);
                    result.Violations.Add(new ComplianceViolation
                    {
                        TenantId = tenantId,
                        Severity = "error",
                        Description = $"Scan failed: {ex.Message}",
                        RuleType = "scan_error"
                    });
                }
            }

            result.CompletedAt = DateTime.UtcNow;
            result.ComplianceRate = result.TotalTenants > 0 
                ? (double)result.CompliantTenants / result.TotalTenants * 100 
                : 100;

            _logger.LogInformation("Completed compliance scan {ScanId}: {Rate:F1}% compliance rate", 
                result.ScanId, result.ComplianceRate);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing compliance scan");
            throw;
        }
    }

    public async Task<RuleTemplateValidationResult> ValidateTemplateLogicAsync(string templateId, string validationLogic)
    {
        var result = new RuleTemplateValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        try
        {
            // Basic syntax validation
            if (string.IsNullOrWhiteSpace(validationLogic))
            {
                result.Warnings.Add("No validation logic provided");
                return result;
            }

            // Check for dangerous operations
            var dangerousPatterns = new[] { "System.", "File.", "Directory.", "Process.", "Environment." };
            foreach (var pattern in dangerousPatterns)
            {
                if (validationLogic.Contains(pattern))
                {
                    result.Errors.Add($"Potentially dangerous operation detected: {pattern}");
                }
            }

            // Validate that logic contains expected variables
            var expectedVariables = new[] { "value", "tenant", "user" };
            var hasValidVariable = expectedVariables.Any(v => validationLogic.Contains(v));
            
            if (!hasValidVariable)
            {
                result.Warnings.Add("Validation logic should reference 'value', 'tenant', or 'user' variables");
            }

            // Try to compile as C# expression (simplified validation)
            if (validationLogic.Contains("=>") || validationLogic.Contains("return"))
            {
                // This is likely a lambda or method - basic structure check
                if (!validationLogic.Trim().EndsWith(";") && !validationLogic.Contains("=>"))
                {
                    result.Errors.Add("Validation logic should end with semicolon or be a lambda expression");
                }
            }

            result.IsValid = result.Errors.Count == 0;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template logic for {TemplateId}", templateId);
            result.IsValid = false;
            result.Errors.Add($"Logic validation failed: {ex.Message}");
            return result;
        }
    }

    public async Task<RuleRecommendationResponse> GetRuleRecommendationsAsync(TenantType tenantType, SubscriptionTier tier)
    {
        try
        {
            var recommendations = new List<RuleRecommendation>();

            // Get all available templates
            var templates = await GetRuleTemplatesAsync();
            
            foreach (var template in templates.Templates)
            {
                // Skip if template requires higher tier
                if (template.MinimumTier > tier)
                    continue;

                var recommendation = new RuleRecommendation
                {
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    Category = template.Category,
                    Priority = GetRecommendationPriority(template, tenantType),
                    RecommendedValue = GetRecommendedValue(template, tenantType, tier),
                    Rationale = GetRecommendationRationale(template, tenantType),
                    IsRequired = IsRuleRequired(template, tenantType),
                    ComplianceImpact = template.RequiresCompliance ? "high" : "low"
                };

                recommendations.Add(recommendation);
            }

            return new RuleRecommendationResponse
            {
                TenantType = tenantType,
                SubscriptionTier = tier,
                Recommendations = recommendations.OrderByDescending(r => r.Priority).ToArray(),
                GeneratedAt = DateTime.UtcNow,
                Summary = $"Generated {recommendations.Count} rule recommendations for {tenantType} tenant"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rule recommendations for {TenantType}/{Tier}", tenantType, tier);
            throw;
        }
    }

    #region Private Helper Methods

    private async Task<RuleTemplate> GenerateSystemRuleTemplateAsync(string ruleType)
    {
        return new RuleTemplate
        {
            Id = ruleType,
            Name = SystemRules.GetRuleDisplayName(ruleType),
            Description = SystemRules.GetRuleDescription(ruleType),
            Category = SystemRules.GetRuleCategory(ruleType),
            ValueType = GetValueTypeForRule(ruleType),
            RequiresCompliance = IsComplianceRule(ruleType),
            MinimumTier = GetMinimumTierForRule(ruleType),
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddYears(-1), // System rules are "old"
            CreatedBy = "system",
            Version = 1
        };
    }

    private async Task<List<RuleTemplate>> LoadAllRuleTemplatesAsync()
    {
        var templates = new List<RuleTemplate>();

        // Add all system rules as templates
        var systemRules = SystemRules.GetAllRules();
        foreach (var rule in systemRules)
        {
            templates.Add(await GenerateSystemRuleTemplateAsync(rule));
        }

        // Add custom templates from storage
        var customTemplates = await LoadCustomRuleTemplatesAsync();
        templates.AddRange(customTemplates);

        return templates;
    }

    private async Task<List<RuleTemplate>> LoadCustomRuleTemplatesAsync()
    {
        // In a real implementation, this would load from database
        return new List<RuleTemplate>();
    }

    private List<RuleTemplate> ApplyFilter(List<RuleTemplate> templates, RuleTemplateFilter? filter)
    {
        if (filter == null)
            return templates;

        var filtered = templates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            filtered = filtered.Where(t => t.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.ValueType.HasValue)
        {
            filtered = filtered.Where(t => t.ValueType == filter.ValueType);
        }

        if (filter.RequiresCompliance.HasValue)
        {
            filtered = filtered.Where(t => t.RequiresCompliance == filter.RequiresCompliance);
        }

        if (filter.MinimumTier.HasValue)
        {
            filtered = filtered.Where(t => t.MinimumTier <= filter.MinimumTier);
        }

        if (filter.IsActive.HasValue)
        {
            filtered = filtered.Where(t => t.IsActive == filter.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLowerInvariant();
            filtered = filtered.Where(t => 
                t.Name.ToLowerInvariant().Contains(term) ||
                t.Description.ToLowerInvariant().Contains(term) ||
                t.Category.ToLowerInvariant().Contains(term));
        }

        return filtered.ToList();
    }

    private CategoryInfo[] GetCategories(List<RuleTemplate> templates)
    {
        return templates
            .GroupBy(t => t.Category)
            .Select(g => new CategoryInfo
            {
                Category = g.Key,
                TemplateCount = g.Count(),
                Description = GetCategoryDescription(g.Key)
            })
            .OrderBy(c => c.Category)
            .ToArray();
    }

    private string GetFilterDescription(RuleTemplateFilter filter)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Category))
            parts.Add($"Category: {filter.Category}");

        if (filter.ValueType.HasValue)
            parts.Add($"Type: {filter.ValueType}");

        if (filter.RequiresCompliance.HasValue)
            parts.Add($"Compliance: {(filter.RequiresCompliance.Value ? "Required" : "Not Required")}");

        if (filter.MinimumTier.HasValue)
            parts.Add($"Max Tier: {filter.MinimumTier}");

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            parts.Add($"Search: '{filter.SearchTerm}'");

        return string.Join(", ", parts);
    }

    private string GenerateTemplateId(string name)
    {
        return name.ToLowerInvariant()
                  .Replace(" ", "_")
                  .Replace("-", "_")
                  .Replace("(", "")
                  .Replace(")", "");
    }

    private string IncrementVersion(string currentVersion)
    {
        if (Version.TryParse(currentVersion, out var version))
        {
            return $"{version.Major}.{version.Minor}.{version.Build + 1}";
        }
        return "1.0.1";
    }

    private RuleValueType GetValueTypeForRule(string ruleType)
    {
        return ruleType switch
        {
            var r when r.Contains("limit") || r.Contains("amount") || r.Contains("threshold") => RuleValueType.Amount,
            var r when r.Contains("count") || r.Contains("term") || r.Contains("period") => RuleValueType.Count,
            var r when r.Contains("grade") => RuleValueType.Grade,
            var r when r.Contains("hours") || r.Contains("delay") => RuleValueType.Duration,
            var r when r.Contains("scope") || r.Contains("access") => RuleValueType.Scope,
            var r when r.Contains("level") || r.Contains("verification") => RuleValueType.Enum,
            _ => RuleValueType.Amount
        };
    }

    private bool IsComplianceRule(string ruleType)
    {
        return ruleType.Contains("audit") || ruleType.Contains("kyc") || ruleType.Contains("aml") || 
               ruleType.Contains("regulatory") || ruleType.Contains("retention");
    }

    private SubscriptionTier GetMinimumTierForRule(string ruleType)
    {
        return ruleType switch
        {
            var r when IsComplianceRule(r) => SubscriptionTier.Professional,
            var r when r.Contains("api") || r.Contains("digital") => SubscriptionTier.Professional,
            _ => SubscriptionTier.Starter
        };
    }

    private bool ValidateValueRange(object minValue, object maxValue, RuleValueType valueType)
    {
        try
        {
            return valueType switch
            {
                RuleValueType.Amount => Convert.ToDecimal(minValue) < Convert.ToDecimal(maxValue),
                RuleValueType.Count => Convert.ToInt32(minValue) < Convert.ToInt32(maxValue),
                _ => true
            };
        }
        catch
        {
            return false;
        }
    }

    private string GetCategoryDescription(string category)
    {
        return category switch
        {
            "Financial" => "Monetary limits and financial constraints",
            "Risk Management" => "Risk assessment and approval limits",
            "Operational" => "Operational constraints and access controls",
            "Compliance" => "Regulatory and audit requirements",
            "PMEC Integration" => "Government payroll system integration rules",
            "Loan Products" => "Product-specific loan configuration",
            "Digital Banking" => "Digital channel and API limitations",
            _ => $"Rules for {category.ToLowerInvariant()} operations"
        };
    }

    // Mock implementations for demonstration
    private async Task SaveRuleTemplateAsync(RuleTemplate template) { }
    private async Task<RuleTemplate?> LoadRuleTemplateAsync(string templateId) => null;
    private async Task<bool> DeleteRuleTemplateFromStorageAsync(string templateId) => true;
    private async Task<int> GetTenantUsageCountAsync(string templateId) => 5;
    private async Task<int> GetRoleUsageCountAsync(string templateId) => 25;
    private async Task<int> GetTotalRulesCreatedAsync(string templateId) => 100;
    private async Task<decimal> GetAverageRuleValueAsync(string templateId) => 50000m;
    private async Task<double> GetComplianceRateAsync(string templateId) => 98.5;
    private async Task<DateTime?> GetLastUsageTimestampAsync(string templateId) => DateTime.UtcNow.AddDays(-2);
    private async Task<string> GetUsageTrendAsync(string templateId) => "increasing";

    private async Task<RuleTemplateAnalytics> GenerateTemplateAnalyticsAsync()
    {
        return new RuleTemplateAnalytics
        {
            TotalTemplates = SystemRules.GetAllRules().Length,
            ActiveTemplates = SystemRules.GetAllRules().Length,
            CategoriesCount = SystemRules.GetRulesByCategory().Count,
            MostUsedTemplate = SystemRules.LoanApprovalLimit,
            OverallComplianceRate = 96.8,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<List<Guid>> GetAffectedTenantsAsync(RuleEngineDeploymentRequest request)
    {
        // Mock implementation
        return new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
    }

    private async Task DeployToTenantAsync(Guid tenantId, RuleEngineDeploymentRequest request) { }

    private async Task<TenantRuleAnalysis> GenerateTenantRuleAnalysisAsync(Guid tenantId)
    {
        return new TenantRuleAnalysis
        {
            TenantId = tenantId,
            TotalRules = 15,
            ActiveRules = 12,
            ComplianceScore = 94.5,
            LastUpdated = DateTime.UtcNow.AddDays(-7),
            RiskLevel = "low",
            RecommendedActions = new[] { "Review unused rules", "Update compliance settings" }
        };
    }

    private async Task<List<Guid>> GetTenantsForComplianceScanAsync(ComplianceScanRequest? request)
    {
        // Mock implementation
        return new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    }

    private async Task<ComplianceResult> CheckTenantComplianceAsync(Guid tenantId)
    {
        return new ComplianceResult
        {
            IsCompliant = true,
            Violations = new List<ComplianceViolation>()
        };
    }

    private int GetRecommendationPriority(RuleTemplate template, TenantType tenantType)
    {
        var priority = template.RequiresCompliance ? 8 : 5;
        
        if (tenantType == TenantType.Bank && template.Category == "Financial")
            priority += 2;
        
        if (template.Category == "Compliance")
            priority += 3;
            
        return Math.Min(10, priority);
    }

    private string GetRecommendedValue(RuleTemplate template, TenantType tenantType, SubscriptionTier tier)
    {
        return template.Id switch
        {
            SystemRules.LoanApprovalLimit => tenantType == TenantType.Bank ? "100000" : "50000",
            SystemRules.MaxRiskGrade => "C",
            SystemRules.RequiredApprovalCount => tier >= SubscriptionTier.Professional ? "2" : "1",
            _ => template.DefaultValue?.ToString() ?? "default"
        };
    }

    private string GetRecommendationRationale(RuleTemplate template, TenantType tenantType)
    {
        return template.Category switch
        {
            "Financial" => $"Recommended based on {tenantType} risk profile and regulatory requirements",
            "Compliance" => "Required for regulatory compliance and audit readiness",
            "Risk Management" => "Helps maintain portfolio quality and minimize defaults",
            _ => "Standard recommendation for operational efficiency"
        };
    }

    private bool IsRuleRequired(RuleTemplate template, TenantType tenantType)
    {
        return template.RequiresCompliance || 
               (tenantType == TenantType.Bank && template.Category == "Financial");
    }

    #endregion
}

// Helper classes
public class ComplianceResult
{
    public bool IsCompliant { get; set; }
    public List<ComplianceViolation> Violations { get; set; } = new();
}