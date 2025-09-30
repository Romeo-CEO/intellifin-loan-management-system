using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.IdentityService.Services;
using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Controllers.Platform;

/// <summary>
/// Platform Plane API for cross-tenant permission analytics
/// Available to IntelliFin internal team with PlatformAdmin role
/// Routes: /platform/v1/permissions/analytics/* and /platform/v1/roles/common-patterns
/// </summary>
[ApiController]
[Route("platform/v1")]
[Authorize(Roles = "PlatformAdmin")]
[Produces("application/json")]
public class PlatformPermissionAnalyticsController : ControllerBase
{
    private readonly IPermissionCatalogService _permissionCatalogService;
    private readonly IPermissionRoleBridgeService _bridgeService;
    private readonly IRoleCompositionService _roleCompositionService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<PlatformPermissionAnalyticsController> _logger;

    public PlatformPermissionAnalyticsController(
        IPermissionCatalogService permissionCatalogService,
        IPermissionRoleBridgeService bridgeService,
        IRoleCompositionService roleCompositionService,
        ITenantResolver tenantResolver,
        ILogger<PlatformPermissionAnalyticsController> logger)
    {
        _permissionCatalogService = permissionCatalogService;
        _bridgeService = bridgeService;
        _roleCompositionService = roleCompositionService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Get permission usage analytics across all tenants
    /// </summary>
    [HttpGet("permissions/analytics")]
    [ProducesResponseType(typeof(PlatformPermissionAnalytics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PlatformPermissionAnalytics>> GetPermissionAnalytics(
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        try
        {
            // Get all permission usage stats
            var usageStats = await _permissionCatalogService.GetPermissionUsageStatsAsync(cancellationToken);
            
            // Convert to platform analytics format
            var permissionUsage = usageStats.Select(stat => new PlatformPermissionUsage
            {
                Permission = stat.PermissionId,
                Name = GetPermissionDisplayName(stat.PermissionId),
                TenantUsage = stat.ActiveTenants,
                TotalAssignments = stat.TotalRoleAssignments,
                AverageRoleAssignments = stat.AverageRoleAssignments,
                Category = GetPermissionCategory(stat.PermissionId),
                AdoptionRate = CalculateAdoptionRate(stat.ActiveTenants)
            }).OrderByDescending(p => p.TenantUsage).ToArray();

            // Generate trends and recommendations
            var trends = GeneratePlatformTrends(permissionUsage);
            var recommendations = GeneratePlatformRecommendations(permissionUsage);
            var summary = GeneratePlatformSummary(permissionUsage);

            var analytics = new PlatformPermissionAnalytics
            {
                PermissionUsage = permissionUsage,
                Trends = trends,
                Recommendations = recommendations,
                Summary = summary
            };

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving platform permission analytics");
            return StatusCode(500, "An error occurred while retrieving analytics");
        }
    }

    /// <summary>
    /// Get common role patterns across tenants
    /// </summary>
    [HttpGet("roles/common-patterns")]
    [ProducesResponseType(typeof(CommonRolePatternsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CommonRolePatternsResponse>> GetCommonRolePatterns(
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        try
        {
            // In a real implementation, this would analyze role patterns across all tenants
            // For now, provide sample common patterns based on typical financial institution structures
            var commonRoles = GenerateCommonRolePatterns();
            var emergingPatterns = GenerateEmergingPatterns();
            var recommendations = GeneratePatternRecommendations(commonRoles);

            var response = new CommonRolePatternsResponse
            {
                CommonRoles = commonRoles,
                EmergingPatterns = emergingPatterns,
                Recommendations = recommendations
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving common role patterns");
            return StatusCode(500, "An error occurred while retrieving role patterns");
        }
    }

    /// <summary>
    /// Get permission health check for a specific tenant
    /// </summary>
    [HttpGet("tenants/{tenantId}/permission-health")]
    [ProducesResponseType(typeof(TenantPermissionHealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantPermissionHealthReport>> GetTenantPermissionHealth(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        try
        {
            var healthReport = await _bridgeService.AnalyzeTenantPermissionHealthAsync(tenantId, cancellationToken);
            return Ok(healthReport);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission health for tenant {TenantId}", tenantId);
            return StatusCode(500, "An error occurred while retrieving tenant health");
        }
    }

    /// <summary>
    /// Generate optimization suggestions for permissions
    /// </summary>
    [HttpPost("permissions/optimization-suggestions")]
    [ProducesResponseType(typeof(PermissionOptimizationSuggestions), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PermissionOptimizationSuggestions>> GetOptimizationSuggestions(
        [FromBody] OptimizationRequest request,
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
            var suggestions = await GenerateOptimizationSuggestions(request, cancellationToken);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating optimization suggestions");
            return StatusCode(500, "An error occurred while generating suggestions");
        }
    }

    /// <summary>
    /// Get detailed analytics for specific permissions
    /// </summary>
    [HttpPost("permissions/detailed-analytics")]
    [ProducesResponseType(typeof(DetailedPermissionAnalytics[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DetailedPermissionAnalytics[]>> GetDetailedPermissionAnalytics(
        [FromBody] string[] permissionIds,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantResolver.IsPlatformPlane())
        {
            return Forbid();
        }

        if (permissionIds == null || permissionIds.Length == 0)
        {
            return BadRequest("Permission IDs are required");
        }

        try
        {
            var analytics = new List<DetailedPermissionAnalytics>();

            foreach (var permissionId in permissionIds)
            {
                var detailed = await GenerateDetailedAnalytics(permissionId, cancellationToken);
                analytics.Add(detailed);
            }

            return Ok(analytics.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating detailed permission analytics");
            return StatusCode(500, "An error occurred while generating detailed analytics");
        }
    }

    #region Private Helper Methods

    private string GetPermissionDisplayName(string permissionId)
    {
        // Convert permission ID to display name
        var parts = permissionId.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0].Replace("_", " ");
            var action = parts[1].Replace("_", " ");
            return $"{char.ToUpper(action[0])}{action[1..]} {char.ToUpper(resource[0])}{resource[1..]}";
        }
        return permissionId;
    }

    private string GetPermissionCategory(string permissionId)
    {
        var parts = permissionId.Split(':');
        if (parts.Length >= 1)
        {
            return parts[0] switch
            {
                "clients" => "Client Management",
                "loans" => "Loan Management",
                "reports" => "Reporting",
                "audit_trail" => "Audit & Compliance",
                "system" => "System Administration",
                "gl" => "General Ledger",
                "payments" => "Payment Processing",
                _ => "General"
            };
        }
        return "General";
    }

    private string CalculateAdoptionRate(int activeTenants)
    {
        // In a real implementation, this would be based on total tenant count
        var totalTenants = 15; // Sample total
        var rate = totalTenants > 0 ? (double)activeTenants / totalTenants * 100 : 0;
        return $"{rate:F1}%";
    }

    private PlatformTrends GeneratePlatformTrends(PlatformPermissionUsage[] permissionUsage)
    {
        return new PlatformTrends
        {
            FastestGrowing = permissionUsage
                .Where(p => p.Category == "Digital Banking" || p.Permission.Contains("mobile") || p.Permission.Contains("digital"))
                .Take(3)
                .Select(p => p.Permission)
                .ToArray(),
            LeastUsed = permissionUsage
                .OrderBy(p => p.TenantUsage)
                .Take(3)
                .Select(p => p.Permission)
                .ToArray(),
            HighestRisk = permissionUsage
                .Where(p => p.Permission.Contains("system") || p.Permission.Contains("delete") || p.Permission.Contains("emergency"))
                .Take(3)
                .Select(p => p.Permission)
                .ToArray()
        };
    }

    private PlatformRecommendation[] GeneratePlatformRecommendations(PlatformPermissionUsage[] permissionUsage)
    {
        var recommendations = new List<PlatformRecommendation>();

        // Find underutilized permissions
        var underutilized = permissionUsage.Where(p => p.TenantUsage < 3 && !p.Permission.Contains("system")).ToArray();
        if (underutilized.Length > 0)
        {
            recommendations.Add(new PlatformRecommendation
            {
                Type = "deprecate",
                Permissions = underutilized.Take(3).Select(p => p.Permission).ToArray(),
                Reason = "Used by < 20% of tenants"
            });
        }

        // Find compliance-critical permissions with low adoption
        var complianceCritical = permissionUsage.Where(p => 
            p.Category == "Audit & Compliance" && p.TenantUsage < 10).ToArray();
        
        if (complianceCritical.Length > 0)
        {
            recommendations.Add(new PlatformRecommendation
            {
                Type = "promote",
                Permissions = complianceCritical.Take(3).Select(p => p.Permission).ToArray(),
                Reason = "High compliance value, low adoption"
            });
        }

        return recommendations.ToArray();
    }

    private PlatformSummary GeneratePlatformSummary(PlatformPermissionUsage[] permissionUsage)
    {
        return new PlatformSummary
        {
            TotalPermissions = permissionUsage.Length,
            ActivePermissions = permissionUsage.Count(p => p.TenantUsage > 0),
            AveragePermissionsPerTenant = permissionUsage.Length > 0 ? permissionUsage.Average(p => p.TotalAssignments) : 0,
            ComplianceScore = 94.2 // Would be calculated based on actual compliance metrics
        };
    }

    private CommonRolePattern[] GenerateCommonRolePatterns()
    {
        return new[]
        {
            new CommonRolePattern
            {
                PatternName = "Standard Loan Officer",
                Frequency = 14,
                TenantAdoption = "93.3%",
                CommonPermissions = new[] { "clients:view", "clients:create", "clients:edit", "loans:create", "loan_applications:process" },
                AveragePermissionCount = 12,
                Variations = new[]
                {
                    new RoleVariation
                    {
                        Name = "Senior Loan Officer",
                        AdditionalPermissions = new[] { "loans:approve" },
                        Frequency = 8
                    }
                }
            },
            new CommonRolePattern
            {
                PatternName = "Credit Analyst",
                Frequency = 12,
                TenantAdoption = "80%",
                CommonPermissions = new[] { "loans:review", "loans:approve", "credit_reports:view", "risk_assessment:perform" },
                AveragePermissionCount = 8,
                ComplianceNotes = "High compliance with BoZ segregation requirements"
            },
            new CommonRolePattern
            {
                PatternName = "Branch Manager",
                Frequency = 15,
                TenantAdoption = "100%",
                CommonPermissions = new[] { "branch:manage", "reports:view", "users:view", "loans:view", "clients:view" },
                AveragePermissionCount = 18,
                ComplianceNotes = "Essential for branch operations oversight"
            }
        };
    }

    private EmergingRolePattern[] GenerateEmergingPatterns()
    {
        return new[]
        {
            new EmergingRolePattern
            {
                PatternName = "Digital Banking Officer",
                Frequency = 3,
                IsGrowing = true,
                UniquePermissions = new[] { "mobile:approve", "digital_payments:process" },
                GrowthRate = "200% in last 6 months"
            },
            new EmergingRolePattern
            {
                PatternName = "Compliance Specialist",
                Frequency = 5,
                IsGrowing = true,
                UniquePermissions = new[] { "compliance:advanced_reports", "audit_trail:advanced" },
                GrowthRate = "150% in last year"
            }
        };
    }

    private PatternRecommendation[] GeneratePatternRecommendations(CommonRolePattern[] patterns)
    {
        return patterns.Where(p => p.Frequency > 10).Select(p => new PatternRecommendation
        {
            Type = "create_template",
            PatternName = p.PatternName,
            Reason = "High frequency pattern with consistent permissions"
        }).ToArray();
    }

    private async Task<PermissionOptimizationSuggestions> GenerateOptimizationSuggestions(
        OptimizationRequest request, 
        CancellationToken cancellationToken)
    {
        var suggestions = new List<OptimizationSuggestion>();

        // Generate suggestions based on request criteria
        if (request.IncludeDeprecationSuggestions)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = "deprecate_unused",
                Title = "Deprecate Unused Permissions",
                Description = "Several permissions are used by less than 10% of tenants",
                Impact = "Low risk, reduces catalog complexity",
                AffectedPermissions = new[] { "legacy:import", "deprecated:old_reports" },
                EstimatedEffort = "Low"
            });
        }

        if (request.IncludeConsolidationSuggestions)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Type = "consolidate_similar",
                Title = "Consolidate Similar Permissions",
                Description = "Some permissions have significant overlap and could be consolidated",
                Impact = "Medium effort, simplifies role management",
                AffectedPermissions = new[] { "reports:view", "reports:basic_view" },
                EstimatedEffort = "Medium"
            });
        }

        return new PermissionOptimizationSuggestions
        {
            Suggestions = suggestions.ToArray(),
            GeneratedAt = DateTime.UtcNow,
            Summary = new OptimizationSummary
            {
                TotalSuggestions = suggestions.Count,
                HighImpactSuggestions = suggestions.Count(s => s.Impact.Contains("High")),
                EstimatedBenefit = "15-20% reduction in permission management overhead"
            }
        };
    }

    private async Task<DetailedPermissionAnalytics> GenerateDetailedAnalytics(
        string permissionId, 
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would query detailed usage data
        return new DetailedPermissionAnalytics
        {
            PermissionId = permissionId,
            PermissionName = GetPermissionDisplayName(permissionId),
            TotalUsage = Random.Shared.Next(50, 200),
            TenantDistribution = GenerateTenantDistribution(),
            UsageTrend = GenerateUsageTrend(),
            RelatedPermissions = GenerateRelatedPermissions(permissionId),
            RiskAssessment = GenerateRiskAssessment(permissionId)
        };
    }

    private TenantUsageDistribution[] GenerateTenantDistribution()
    {
        return new[]
        {
            new TenantUsageDistribution { TenantSize = "Small (1-10 users)", Count = 8, Percentage = 53.3 },
            new TenantUsageDistribution { TenantSize = "Medium (11-50 users)", Count = 5, Percentage = 33.3 },
            new TenantUsageDistribution { TenantSize = "Large (50+ users)", Count = 2, Percentage = 13.3 }
        };
    }

    private UsageTrendPoint[] GenerateUsageTrend()
    {
        var trends = new List<UsageTrendPoint>();
        var baseDate = DateTime.UtcNow.AddMonths(-6);
        
        for (int i = 0; i < 6; i++)
        {
            trends.Add(new UsageTrendPoint
            {
                Date = baseDate.AddMonths(i),
                Usage = Random.Shared.Next(20, 50)
            });
        }

        return trends.ToArray();
    }

    private string[] GenerateRelatedPermissions(string permissionId)
    {
        // Generate related permissions based on the input permission
        var parts = permissionId.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0];
            return new[] { $"{resource}:create", $"{resource}:edit", $"{resource}:delete" }
                .Where(p => p != permissionId)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private PermissionRiskAssessment GenerateRiskAssessment(string permissionId)
    {
        var riskLevel = permissionId.Contains("system") || permissionId.Contains("delete") ? "High" :
                       permissionId.Contains("approve") || permissionId.Contains("edit") ? "Medium" : "Low";

        return new PermissionRiskAssessment
        {
            RiskLevel = riskLevel,
            RiskFactors = GenerateRiskFactors(permissionId),
            MitigationRecommendations = GenerateMitigationRecommendations(riskLevel)
        };
    }

    private string[] GenerateRiskFactors(string permissionId)
    {
        var factors = new List<string>();

        if (permissionId.Contains("system"))
            factors.Add("System-level access");
        if (permissionId.Contains("approve"))
            factors.Add("Financial approval authority");
        if (permissionId.Contains("delete"))
            factors.Add("Data deletion capability");

        return factors.ToArray();
    }

    private string[] GenerateMitigationRecommendations(string riskLevel)
    {
        return riskLevel switch
        {
            "High" => new[] { "Implement additional approval workflows", "Enable enhanced audit logging", "Regular access reviews" },
            "Medium" => new[] { "Periodic access reviews", "Basic audit logging" },
            "Low" => new[] { "Standard monitoring" },
            _ => Array.Empty<string>()
        };
    }

    #endregion
}

#region Response Models

/// <summary>
/// Platform permission analytics response
/// </summary>
public class PlatformPermissionAnalytics
{
    public PlatformPermissionUsage[] PermissionUsage { get; set; } = Array.Empty<PlatformPermissionUsage>();
    public PlatformTrends Trends { get; set; } = new();
    public PlatformRecommendation[] Recommendations { get; set; } = Array.Empty<PlatformRecommendation>();
    public PlatformSummary Summary { get; set; } = new();
}

/// <summary>
/// Platform permission usage information
/// </summary>
public class PlatformPermissionUsage
{
    public string Permission { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TenantUsage { get; set; }
    public int TotalAssignments { get; set; }
    public double AverageRoleAssignments { get; set; }
    public string Category { get; set; } = string.Empty;
    public string AdoptionRate { get; set; } = string.Empty;
}

/// <summary>
/// Platform trends information
/// </summary>
public class PlatformTrends
{
    public string[] FastestGrowing { get; set; } = Array.Empty<string>();
    public string[] LeastUsed { get; set; } = Array.Empty<string>();
    public string[] HighestRisk { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Platform recommendation
/// </summary>
public class PlatformRecommendation
{
    public string Type { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Platform summary statistics
/// </summary>
public class PlatformSummary
{
    public int TotalPermissions { get; set; }
    public int ActivePermissions { get; set; }
    public double AveragePermissionsPerTenant { get; set; }
    public double ComplianceScore { get; set; }
}

/// <summary>
/// Common role patterns response
/// </summary>
public class CommonRolePatternsResponse
{
    public CommonRolePattern[] CommonRoles { get; set; } = Array.Empty<CommonRolePattern>();
    public EmergingRolePattern[] EmergingPatterns { get; set; } = Array.Empty<EmergingRolePattern>();
    public PatternRecommendation[] Recommendations { get; set; } = Array.Empty<PatternRecommendation>();
}

/// <summary>
/// Common role pattern information
/// </summary>
public class CommonRolePattern
{
    public string PatternName { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public string TenantAdoption { get; set; } = string.Empty;
    public string[] CommonPermissions { get; set; } = Array.Empty<string>();
    public int AveragePermissionCount { get; set; }
    public RoleVariation[] Variations { get; set; } = Array.Empty<RoleVariation>();
    public string? ComplianceNotes { get; set; }
}

/// <summary>
/// Role variation within a pattern
/// </summary>
public class RoleVariation
{
    public string Name { get; set; } = string.Empty;
    public string[] AdditionalPermissions { get; set; } = Array.Empty<string>();
    public int Frequency { get; set; }
}

/// <summary>
/// Emerging role pattern
/// </summary>
public class EmergingRolePattern
{
    public string PatternName { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public bool IsGrowing { get; set; }
    public string[] UniquePermissions { get; set; } = Array.Empty<string>();
    public string GrowthRate { get; set; } = string.Empty;
}

/// <summary>
/// Pattern recommendation
/// </summary>
public class PatternRecommendation
{
    public string Type { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Optimization request model
/// </summary>
public class OptimizationRequest
{
    public bool IncludeDeprecationSuggestions { get; set; } = true;
    public bool IncludeConsolidationSuggestions { get; set; } = true;
    public bool IncludeSecurityOptimizations { get; set; } = true;
    public string[]? FocusAreas { get; set; }
}

/// <summary>
/// Permission optimization suggestions response
/// </summary>
public class PermissionOptimizationSuggestions
{
    public OptimizationSuggestion[] Suggestions { get; set; } = Array.Empty<OptimizationSuggestion>();
    public DateTime GeneratedAt { get; set; }
    public OptimizationSummary Summary { get; set; } = new();
}

/// <summary>
/// Individual optimization suggestion
/// </summary>
public class OptimizationSuggestion
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string[] AffectedPermissions { get; set; } = Array.Empty<string>();
    public string EstimatedEffort { get; set; } = string.Empty;
}

/// <summary>
/// Optimization summary
/// </summary>
public class OptimizationSummary
{
    public int TotalSuggestions { get; set; }
    public int HighImpactSuggestions { get; set; }
    public string EstimatedBenefit { get; set; } = string.Empty;
}

/// <summary>
/// Detailed permission analytics
/// </summary>
public class DetailedPermissionAnalytics
{
    public string PermissionId { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public int TotalUsage { get; set; }
    public TenantUsageDistribution[] TenantDistribution { get; set; } = Array.Empty<TenantUsageDistribution>();
    public UsageTrendPoint[] UsageTrend { get; set; } = Array.Empty<UsageTrendPoint>();
    public string[] RelatedPermissions { get; set; } = Array.Empty<string>();
    public PermissionRiskAssessment RiskAssessment { get; set; } = new();
}

/// <summary>
/// Tenant usage distribution
/// </summary>
public class TenantUsageDistribution
{
    public string TenantSize { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Usage trend point
/// </summary>
public class UsageTrendPoint
{
    public DateTime Date { get; set; }
    public int Usage { get; set; }
}

/// <summary>
/// Permission risk assessment
/// </summary>
public class PermissionRiskAssessment
{
    public string RiskLevel { get; set; } = string.Empty;
    public string[] RiskFactors { get; set; } = Array.Empty<string>();
    public string[] MitigationRecommendations { get; set; } = Array.Empty<string>();
}

#endregion