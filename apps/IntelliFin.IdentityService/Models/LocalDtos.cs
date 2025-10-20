namespace IntelliFin.IdentityService.Models;

// General severity for issues
public enum IssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}

// Compliance result models
public class ComplianceValidationResult
{
    public bool IsCompliant { get; set; }
    public int OverallScore { get; set; }
    public ComplianceCheck[] Checks { get; set; } = Array.Empty<ComplianceCheck>();
    public ComplianceIssue[] ComplianceIssues { get; set; } = Array.Empty<ComplianceIssue>();
    public ComplianceWarning[] Warnings { get; set; } = Array.Empty<ComplianceWarning>();
    public ComplianceRecommendation[] Recommendations { get; set; } = Array.Empty<ComplianceRecommendation>();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

public class ComplianceCheck
{
    public string CheckType { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Score { get; set; }
    public double Weight { get; set; } = 1.0;
}

public class ComplianceIssue
{
    public IssueSeverity Severity { get; set; } = IssueSeverity.Low;
    public string Issue { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
}

public class ComplianceWarning
{
    public string Message { get; set; } = string.Empty;
}

public class ComplianceRecommendation
{
    public string Reason { get; set; } = string.Empty;
}

// Role composition request models
public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoleCategory Category { get; set; } = RoleCategory.Operations;
    public string[] InitialPermissions { get; set; } = Array.Empty<string>();
    public string? TemplateId { get; set; }
}

public class UpdateRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public RoleCategory? Category { get; set; }
    public bool? IsActive { get; set; }
}

public class AddPermissionsToRoleRequest
{
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? Notes { get; set; }
}

public class BulkUpdateRolePermissionsRequest
{
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? Notes { get; set; }
}

// Tenant role listing models
public class TenantRoleSummary
{
    public int TotalRoles { get; set; }
    public int ActiveRoles { get; set; }
    public int TotalUsers { get; set; }
    public int ComplianceIssues { get; set; }
    public double AverageComplianceScore { get; set; }
    public DateTime LastRoleCreated { get; set; }
    public DateTime? LastRoleModified { get; set; }
}

public class TenantRoleContext
{
    public string TenantId { get; set; } = string.Empty;
    public bool CanCreateRoles { get; set; } = true;
    public int MaxRoles { get; set; } = 20;
    public int RolesUsed { get; set; }
    public string SubscriptionTier { get; set; } = string.Empty;
    public string[] EnabledFeatures { get; set; } = Array.Empty<string>();
}

// Permission bridge and analytics models
public enum AssignmentSource
{
    Manual,
    Bulk,
    Template
}

public class PermissionValidationResult
{
    public string ComplianceCheck { get; set; } = string.Empty; // "passed" | "warning" | "failed"
    public string[] Conflicts { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}

public class PermissionAssignmentResult
{
    public string Permission { get; set; } = string.Empty;
    public bool Added { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RolePermissionResult
{
    public string RoleId { get; set; } = string.Empty;
    public PermissionAssignmentResult[] AddedPermissions { get; set; } = Array.Empty<PermissionAssignmentResult>();
    public int TotalPermissions { get; set; }
    public ComplianceValidationResult ValidationResults { get; set; } = new();
    public bool IsSuccess { get; set; }
}

public class BridgeOperationResult
{
    public string RoleId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public int RolePermissionCount { get; set; }
    public int AffectedUsers { get; set; }
    public PermissionValidationResult? ValidationResults { get; set; }

    public static BridgeOperationResult CreateSuccess(string roleId, string permission, string performedBy)
    {
        return new BridgeOperationResult
        {
            RoleId = roleId,
            Permission = permission,
            Success = true,
            Message = "ok",
            PerformedBy = performedBy
        };
    }

    public static BridgeOperationResult Failure(string roleId, string permission, string message)
    {
        return new BridgeOperationResult
        {
            RoleId = roleId,
            Permission = permission,
            Success = false,
            Message = message
        };
    }
}

public class BulkPermissionAssignmentRequest
{
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public AssignmentSource Source { get; set; } = AssignmentSource.Bulk;
    public string? Notes { get; set; }
    public string? TemplateId { get; set; }
}

public class ReplacePermissionsRequest
{
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? Notes { get; set; }
}

public class BulkBridgeOperationResult
{
    public string RoleId { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public int NewPermissionCount { get; set; }
    public AssignmentSource Source { get; set; } = AssignmentSource.Bulk;
    public string? TemplateId { get; set; }
    public PermissionValidationResult? ValidationResults { get; set; }
    public PermissionAssignmentResult[] Results { get; set; } = Array.Empty<PermissionAssignmentResult>();

    public static BulkBridgeOperationResult BulkSuccess(PermissionAssignmentResult[] results)
    {
        return new BulkBridgeOperationResult
        {
            Results = results
        };
    }
}

public class AvailablePermission
{
    public string Permission { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public bool RequiresUpgrade { get; set; }
    public int RecommendationScore { get; set; }
    public bool IsCommonlyUsed { get; set; }
}

public class TenantPermissionUsage
{
    public string Permission { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int RoleCount { get; set; }
    public int UserCount { get; set; }
    public double Coverage { get; set; }
    public DateTime? FirstAssigned { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class RolePermissionMatrixResponse
{
    public RolePermissionMatrix[] Matrix { get; set; } = Array.Empty<RolePermissionMatrix>();
    public TenantPermissionUsage[] PermissionSummary { get; set; } = Array.Empty<TenantPermissionUsage>();
    public PermissionMatrixSummary TenantStats { get; set; } = new();
    public MatrixFilterInfo Filters { get; set; } = new();
}

public class RolePermissionMatrix
{
    public RoleMatrixInfo Role { get; set; } = new();
    public PermissionMatrixEntry[] Permissions { get; set; } = Array.Empty<PermissionMatrixEntry>();
    public int PermissionCount { get; set; }
    public int UserCount { get; set; }
}

public class RoleMatrixInfo
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public RoleCategory Category { get; set; }
}

public class PermissionMatrixEntry
{
    public string Permission { get; set; } = string.Empty;
    public bool HasPermission { get; set; }
    public DateTime? AssignedAt { get; set; }
    public AssignmentSource Source { get; set; } = AssignmentSource.Manual;
}

public class PermissionMatrixSummary
{
    public int TotalRoles { get; set; }
    public int TotalPermissions { get; set; }
    public int TotalUsers { get; set; }
    public double AveragePermissionsPerRole { get; set; }
    public TenantPermissionUsage[] TopPermissions { get; set; } = Array.Empty<TenantPermissionUsage>();
}

public class MatrixFilterInfo
{
    public string[] PermissionFilter { get; set; } = Array.Empty<string>();
    public RoleCategory? RoleCategory { get; set; }
    public bool ExcludeInactive { get; set; }
}

public class PermissionAssignment
{
    public string RoleId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public AssignmentSource Source { get; set; } = AssignmentSource.Manual;
    public bool IsActive { get; set; }
    public ApplicationRole Role { get; set; } = null!;
}

public class PermissionChangeEntry
{
    public string ChangeType { get; set; } = string.Empty; // e.g., assigned/removed
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public AssignmentSource Source { get; set; } = AssignmentSource.Manual;
    public int AffectedUsers { get; set; }
}

// Tenant permission health report models
public class TenantPermissionHealthReport
{
    public string TenantId { get; set; } = string.Empty;
    public int OverallHealthScore { get; set; }
    public PermissionHealthAssessment Assessment { get; set; } = new();
    public PermissionOptimization[] Optimizations { get; set; } = Array.Empty<PermissionOptimization>();
    public PermissionComplianceIssue[] ComplianceIssues { get; set; } = Array.Empty<PermissionComplianceIssue>();
    public string[] UnusedPermissions { get; set; } = Array.Empty<string>();
    public string[] OverPrivilegedRoles { get; set; } = Array.Empty<string>();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}

public class PermissionHealthAssessment
{
    public int ComplianceScore { get; set; }
    public int SegregationScore { get; set; }
    public int EfficiencyScore { get; set; }
    public int SecurityScore { get; set; }
    public int TotalAssignments { get; set; }
    public int RedundantAssignments { get; set; }
    public int HighRiskAssignments { get; set; }
    public double OverPrivilegedRoleRate { get; set; }
}

public class PermissionOptimization
{
    public string OptimizationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string[] AffectedRoles { get; set; } = Array.Empty<string>();
    public string Effort { get; set; } = string.Empty;
    public string ExpectedBenefit { get; set; } = string.Empty;
}

public class PermissionComplianceIssue
{
    public IssueSeverity Severity { get; set; } = IssueSeverity.Low;
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public ComplianceFramework Framework { get; set; } = ComplianceFramework.BoZ;
}