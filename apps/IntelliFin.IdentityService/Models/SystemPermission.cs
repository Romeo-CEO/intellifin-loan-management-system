namespace IntelliFin.IdentityService.Models;

/// <summary>
/// Represents a system permission with metadata for tenant discovery and management
/// </summary>
public class SystemPermission
{
    /// <summary>
    /// Unique permission identifier (e.g., "clients:view")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name (e.g., "View Client Information")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what this permission grants
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for UI organization (e.g., "Client Management")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Resource type this permission applies to
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// Action type (e.g., "view", "create", "approve")
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Feature flags required for this permission to be available
    /// </summary>
    public string[] RequiredFeatures { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum subscription tier required
    /// </summary>
    public SubscriptionTier MinimumTier { get; set; } = SubscriptionTier.Starter;

    /// <summary>
    /// Risk level of this permission
    /// </summary>
    public PermissionRiskLevel RiskLevel { get; set; } = PermissionRiskLevel.Low;

    /// <summary>
    /// Whether this permission supports rule-based claims with values
    /// </summary>
    public bool SupportsValueClaims { get; set; }

    /// <summary>
    /// Common rule types for this permission (e.g., approval limits)
    /// </summary>
    public string[] CommonRules { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this permission is deprecated
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// When this permission was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created this permission
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Risk levels for permissions
/// </summary>
public enum PermissionRiskLevel
{
    Low = 1,        // View operations, basic access
    Medium = 2,     // Create/Edit operations
    High = 3,       // Delete/Financial operations, approvals
    Critical = 4    // System configuration, emergency access
}

/// <summary>
/// Subscription tiers that determine permission availability
/// </summary>
public enum SubscriptionTier
{
    Starter = 1,
    Professional = 2,
    Enterprise = 3
}

/// <summary>
/// Permission category information for UI organization
/// </summary>
public class PermissionCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PermissionCount { get; set; }
    public PermissionRiskLevel MaxRiskLevel { get; set; }
    public string[] RestrictedPermissions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Permission usage analytics
/// </summary>
public class PermissionUsageStats
{
    public string PermissionId { get; set; } = string.Empty;
    public int ActiveTenants { get; set; }
    public int TotalRoleAssignments { get; set; }
    public double AverageRoleAssignments { get; set; }
    public double AdoptionRate { get; set; }
    public DateTime LastUsed { get; set; }
}