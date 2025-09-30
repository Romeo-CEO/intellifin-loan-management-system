using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

/// <summary>
/// Platform Plane API for managing rule templates (IntelliFin internal use)
/// </summary>
[ApiController]
[Route("api/platform/v1/rule-templates")]
[Authorize(Roles = "PlatformAdmin")]
public class PlatformRuleTemplateController : ControllerBase
{
    private readonly IRuleTemplateService _ruleTemplateService;
    private readonly ILogger<PlatformRuleTemplateController> _logger;

    public PlatformRuleTemplateController(
        IRuleTemplateService ruleTemplateService,
        ILogger<PlatformRuleTemplateController> logger)
    {
        _ruleTemplateService = ruleTemplateService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new rule template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RuleTemplate>> CreateRuleTemplate([FromBody] CreateRuleTemplateRequest request)
    {
        try
        {
            var template = await _ruleTemplateService.CreateRuleTemplateAsync(request);

            _logger.LogInformation("Created rule template {TemplateId} by {CreatedBy}", 
                template.Id, request.CreatedBy);

            return CreatedAtAction(nameof(GetRuleTemplate), new { templateId = template.Id }, template);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid rule template creation request: {Error}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rule template");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing rule template
    /// </summary>
    [HttpPut("{templateId}")]
    public async Task<ActionResult<RuleTemplate>> UpdateRuleTemplate(
        string templateId,
        [FromBody] UpdateRuleTemplateRequest request)
    {
        try
        {
            var template = await _ruleTemplateService.UpdateRuleTemplateAsync(templateId, request);

            _logger.LogInformation("Updated rule template {TemplateId} to version {Version} by {UpdatedBy}", 
                templateId, template.Version, request.UpdatedBy);

            return Ok(template);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid rule template update request for {TemplateId}: {Error}", templateId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rule template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific rule template
    /// </summary>
    [HttpGet("{templateId}")]
    public async Task<ActionResult<RuleTemplate>> GetRuleTemplate(string templateId)
    {
        try
        {
            var template = await _ruleTemplateService.GetRuleTemplateAsync(templateId);
            
            if (template == null)
            {
                return NotFound(new { message = $"Rule template {templateId} not found" });
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rule template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all available rule templates with filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PlatformRuleTemplateListResponse>> GetRuleTemplates(
        [FromQuery] string? category = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] RuleValueType? valueType = null,
        [FromQuery] bool? requiresCompliance = null,
        [FromQuery] SubscriptionTier? minimumTier = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var filter = new RuleTemplateFilter
            {
                Category = category,
                SearchTerm = searchTerm,
                ValueType = valueType,
                RequiresCompliance = requiresCompliance,
                MinimumTier = minimumTier,
                IsActive = isActive
            };

            var templates = await _ruleTemplateService.GetRuleTemplatesAsync(filter);
            var templatesByCategory = await _ruleTemplateService.GetRuleTemplatesByCategoryAsync();

            // Apply pagination
            var totalCount = templates.Templates.Length;
            var paginatedTemplates = templates.Templates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            // Process templates with async operations
            var processedTemplates = new List<PlatformRuleTemplateDetail>();
            foreach (var t in paginatedTemplates)
            {
                processedTemplates.Add(new PlatformRuleTemplateDetail
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    Category = t.Category,
                    ValueType = t.ValueType,
                    MinValue = t.MinValue,
                    MaxValue = t.MaxValue,
                    AllowedValues = t.AllowedValues,
                    DefaultValue = t.DefaultValue?.ToString(),
                    ValidationLogic = t.ValidationLogic,
                    RequiresCompliance = t.RequiresCompliance,
                    MinimumTier = t.MinimumTier,
                    IsActive = t.IsActive,
                    Version = t.Version.ToString(),
                    CreatedAt = t.CreatedAt.DateTime,
                    CreatedBy = t.CreatedBy,
                    UpdatedAt = t.UpdatedAt?.DateTime,
                    UpdatedBy = t.UpdatedBy,
                    UsageStats = await GetTemplateUsageStatsAsync(t.Id)
                });
            }

            // Process categories with async operations
            var processedCategories = new List<PlatformTemplateCategoryInfo>();
            foreach (var kvp in templatesByCategory)
            {
                processedCategories.Add(new PlatformTemplateCategoryInfo
                {
                    Category = kvp.Key,
                    TemplateCount = kvp.Value.Length,
                    Description = GetCategoryDescription(kvp.Key),
                    AverageUsage = await GetCategoryAverageUsageAsync(kvp.Key),
                    ComplianceRate = await GetCategoryComplianceRateAsync(kvp.Key)
                });
            }

            var response = new PlatformRuleTemplateListResponse
            {
                Templates = processedTemplates.ToArray(),
                Categories = processedCategories.ToArray(),
                Pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalCount = totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    hasNextPage = page * pageSize < totalCount,
                    hasPreviousPage = page > 1
                },
                Statistics = new
                {
                    totalTemplates = totalCount,
                    activeTemplates = templates.Templates.Count(t => t.IsActive),
                    complianceTemplates = templates.Templates.Count(t => t.RequiresCompliance),
                    categoriesCount = templatesByCategory.Count
                },
                FilterInfo = BuildFilterInfo(filter)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rule templates");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a rule template
    /// </summary>
    [HttpDelete("{templateId}")]
    public async Task<ActionResult> DeleteRuleTemplate(string templateId)
    {
        try
        {
            var deleted = await _ruleTemplateService.DeleteRuleTemplateAsync(templateId);
            
            if (!deleted)
            {
                return NotFound(new { message = $"Rule template {templateId} not found" });
            }

            _logger.LogInformation("Deleted rule template {TemplateId}", templateId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cannot delete rule template {TemplateId}: {Error}", templateId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rule template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Validates a rule template configuration
    /// </summary>
    [HttpPost("{templateId}/validate")]
    public async Task<ActionResult<RuleTemplateValidationResponse>> ValidateRuleTemplate(
        string templateId,
        [FromBody] ValidateRuleTemplateRequest request)
    {
        try
        {
            var template = await _ruleTemplateService.GetRuleTemplateAsync(templateId);
            if (template == null)
            {
                return NotFound(new { message = $"Rule template {templateId} not found" });
            }

            // Update template with request data if provided
            var templateToValidate = new RuleTemplate
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                Category = template.Category,
                ValueType = template.ValueType,
                ValidationLogic = request.ValidationLogic ?? template.ValidationLogic,
                MinValue = request.MinValue ?? template.MinValue,
                MaxValue = request.MaxValue ?? template.MaxValue,
                AllowedValues = request.AllowedValues ?? template.AllowedValues,
                DefaultValue = template.DefaultValue,
                RequiresCompliance = template.RequiresCompliance,
                MinimumTier = template.MinimumTier,
                RequiredFeatures = template.RequiredFeatures,
                IsActive = template.IsActive,
                CreatedAt = template.CreatedAt,
                CreatedBy = template.CreatedBy,
                Version = template.Version,
                UpdatedAt = template.UpdatedAt,
                UpdatedBy = template.UpdatedBy,
                UsageStats = template.UsageStats,
                RegulatoryMax = template.RegulatoryMax,
                Currency = template.Currency,
                RiskLevel = template.RiskLevel
            };

            var validation = await _ruleTemplateService.ValidateRuleTemplateAsync(templateToValidate);

            // Also validate the validation logic separately if provided
            RuleTemplateValidationResult? logicValidation = null;
            if (!string.IsNullOrWhiteSpace(request.ValidationLogic))
            {
                logicValidation = await _ruleTemplateService.ValidateTemplateLogicAsync(templateId, request.ValidationLogic);
            }

            var response = new RuleTemplateValidationResponse
            {
                TemplateId = templateId,
                IsValid = validation.IsValid && (logicValidation?.IsValid ?? true),
                Errors = validation.Errors.Concat(logicValidation?.Errors ?? Enumerable.Empty<string>()).ToArray(),
                Warnings = validation.Warnings.Concat(logicValidation?.Warnings ?? Enumerable.Empty<string>()).ToArray(),
                ValidationDetails = new
                {
                    templateValidation = new
                    {
                        isValid = validation.IsValid,
                        errors = validation.Errors,
                        warnings = validation.Warnings
                    },
                    logicValidation = logicValidation != null ? new
                    {
                        isValid = logicValidation.IsValid,
                        errors = logicValidation.Errors,
                        warnings = logicValidation.Warnings
                    } : null
                },
                Recommendations = GetValidationRecommendations(validation, logicValidation),
                ValidatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Validated rule template {TemplateId}: {IsValid}", templateId, response.IsValid);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rule template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets comprehensive usage statistics for a rule template
    /// </summary>
    [HttpGet("{templateId}/usage-stats")]
    public async Task<ActionResult<PlatformRuleTemplateUsageResponse>> GetTemplateUsageStats(string templateId)
    {
        try
        {
            var template = await _ruleTemplateService.GetRuleTemplateAsync(templateId);
            if (template == null)
            {
                return NotFound(new { message = $"Rule template {templateId} not found" });
            }

            var usage = await _ruleTemplateService.GetTemplateUsageStatsAsync(templateId);

            var response = new PlatformRuleTemplateUsageResponse
            {
                TemplateId = templateId,
                TemplateName = template.Name,
                Category = template.Category,
                UsageStatistics = new
                {
                    tenantsUsing = usage.TenantsUsing,
                    totalRoles = usage.TotalRoles,
                    averageValue = usage.AverageValue,
                    complianceRate = usage.ComplianceRate,
                    lastUsed = usage.LastUsed,
                    usageTrend = usage.UsageTrend
                },
                TenantBreakdown = await GetTenantUsageBreakdownAsync(templateId),
                ValueDistribution = await GetValueDistributionAsync(templateId),
                ComplianceMetrics = await GetComplianceMetricsAsync(templateId),
                PerformanceMetrics = await GetPerformanceMetricsAsync(templateId),
                GeneratedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage stats for template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets aggregated analytics for all rule templates
    /// </summary>
    [HttpGet("analytics")]
    public async Task<ActionResult<PlatformRuleTemplateAnalyticsResponse>> GetTemplateAnalytics()
    {
        try
        {
            var analytics = await _ruleTemplateService.GetTemplateAnalyticsAsync();

            var response = new PlatformRuleTemplateAnalyticsResponse
            {
                Summary = new
                {
                    totalTemplates = analytics.TotalTemplates,
                    activeTemplates = analytics.ActiveTemplates,
                    categoriesCount = analytics.CategoriesCount,
                    overallComplianceRate = analytics.OverallComplianceRate
                },
                Usage = new
                {
                    mostUsedTemplate = analytics.MostUsedTemplate,
                    averageUsagePerTemplate = await GetAverageUsagePerTemplateAsync(),
                    totalTenantImplementations = await GetTotalTenantImplementationsAsync()
                },
                Compliance = new
                {
                    overallComplianceRate = analytics.OverallComplianceRate,
                    highRiskTemplates = await GetHighRiskTemplatesAsync(),
                    complianceViolations = await GetComplianceViolationsSummaryAsync()
                },
                Performance = new
                {
                    averageEvaluationTime = await GetAverageEvaluationTimeAsync(),
                    performanceBottlenecks = await GetPerformanceBottlenecksAsync()
                },
                Trends = new
                {
                    adoption = await GetAdoptionTrendsAsync(),
                    usage = await GetUsageTrendsAsync(),
                    compliance = await GetComplianceTrendsAsync()
                },
                GeneratedAt = analytics.GeneratedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template analytics");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    #region Private Helper Methods

    private async Task<object> GetTemplateUsageStatsAsync(string templateId)
    {
        var stats = await _ruleTemplateService.GetTemplateUsageStatsAsync(templateId);
        return new
        {
            tenantsUsing = stats.TenantsUsing,
            totalRoles = stats.TotalRoles,
            averageValue = stats.AverageValue,
            complianceRate = stats.ComplianceRate,
            lastUsed = stats.LastUsed,
            trend = stats.UsageTrend
        };
    }

    private string GetCategoryDescription(string category)
    {
        return category switch
        {
            "Financial" => "Monetary limits and financial constraints for transaction and approval workflows",
            "Risk Management" => "Risk assessment parameters and credit evaluation constraints",
            "Operational" => "Day-to-day operational limits and access control parameters",
            "Compliance" => "Regulatory compliance requirements and audit trail configurations",
            "PMEC Integration" => "Government payroll system integration and employee verification rules",
            "Loan Products" => "Product-specific loan configuration and term management",
            "Digital Banking" => "Digital channel limitations and API rate controls",
            _ => $"Configuration templates for {category.ToLowerInvariant()} operations"
        };
    }

    private async Task<double> GetCategoryAverageUsageAsync(string category)
    {
        // Mock implementation - would calculate from actual data
        return category switch
        {
            "Financial" => 85.5,
            "Risk Management" => 72.3,
            "Compliance" => 95.8,
            _ => 65.0
        };
    }

    private async Task<double> GetCategoryComplianceRateAsync(string category)
    {
        // Mock implementation - would calculate from actual data
        return category switch
        {
            "Compliance" => 98.9,
            "Financial" => 96.2,
            "Risk Management" => 94.7,
            _ => 92.0
        };
    }

    private object BuildFilterInfo(RuleTemplateFilter filter)
    {
        var appliedFilters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Category))
            appliedFilters.Add($"Category: {filter.Category}");

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            appliedFilters.Add($"Search: '{filter.SearchTerm}'");

        if (filter.ValueType.HasValue)
            appliedFilters.Add($"Value Type: {filter.ValueType}");

        if (filter.RequiresCompliance.HasValue)
            appliedFilters.Add($"Compliance: {(filter.RequiresCompliance.Value ? "Required" : "Optional")}");

        if (filter.MinimumTier.HasValue)
            appliedFilters.Add($"Min Tier: {filter.MinimumTier}");

        if (filter.IsActive.HasValue)
            appliedFilters.Add($"Status: {(filter.IsActive.Value ? "Active" : "Inactive")}");

        return new
        {
            hasFilters = appliedFilters.Count > 0,
            appliedFilters = appliedFilters.ToArray(),
            description = appliedFilters.Count > 0 
                ? string.Join(", ", appliedFilters) 
                : "No filters applied"
        };
    }

    private string[] GetValidationRecommendations(
        RuleTemplateValidationResult templateValidation, 
        RuleTemplateValidationResult? logicValidation)
    {
        var recommendations = new List<string>();

        if (!templateValidation.IsValid)
        {
            recommendations.Add("Review and fix template configuration errors before deploying");
        }

        if (logicValidation != null && !logicValidation.IsValid)
        {
            recommendations.Add("Validation logic contains errors that must be resolved");
        }

        if (templateValidation.Warnings.Any() || (logicValidation?.Warnings.Any() ?? false))
        {
            recommendations.Add("Consider addressing warnings to improve template quality");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Template validation passed - ready for deployment");
        }

        return recommendations.ToArray();
    }

    // Mock implementations for analytics data
    private async Task<object[]> GetTenantUsageBreakdownAsync(string templateId) => Array.Empty<object>();
    private async Task<object> GetValueDistributionAsync(string templateId) => new { };
    private async Task<object> GetComplianceMetricsAsync(string templateId) => new { };
    private async Task<object> GetPerformanceMetricsAsync(string templateId) => new { };
    private async Task<double> GetAverageUsagePerTemplateAsync() => 12.5;
    private async Task<int> GetTotalTenantImplementationsAsync() => 45;
    private async Task<string[]> GetHighRiskTemplatesAsync() => Array.Empty<string>();
    private async Task<object> GetComplianceViolationsSummaryAsync() => new { };
    private async Task<double> GetAverageEvaluationTimeAsync() => 25.8;
    private async Task<string[]> GetPerformanceBottlenecksAsync() => Array.Empty<string>();
    private async Task<object> GetAdoptionTrendsAsync() => new { };
    private async Task<object> GetUsageTrendsAsync() => new { };
    private async Task<object> GetComplianceTrendsAsync() => new { };

    #endregion
}