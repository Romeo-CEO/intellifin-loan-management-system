using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Constants;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Implementation of permission-role bridge service
/// Manages the assignment and removal of permissions from roles with comprehensive validation
/// </summary>
public class PermissionRoleBridgeService : IPermissionRoleBridgeService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPermissionCatalogService _permissionCatalogService;
    private readonly IRoleCompositionService _roleCompositionService;
    private readonly ITenantResolver _tenantResolver;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionRoleBridgeService> _logger;

    public PermissionRoleBridgeService(
        RoleManager<ApplicationRole> roleManager,
        IPermissionCatalogService permissionCatalogService,
        IRoleCompositionService roleCompositionService,
        ITenantResolver tenantResolver,
        IMemoryCache cache,
        ILogger<PermissionRoleBridgeService> logger)
    {
        _roleManager = roleManager;
        _permissionCatalogService = permissionCatalogService;
        _roleCompositionService = roleCompositionService;
        _tenantResolver = tenantResolver;
        _cache = cache;
        _logger = logger;
    }

    public async Task<BridgeOperationResult> AssignPermissionAsync(
        string roleId,
        string permission,
        string tenantId,
        string assignedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate role ownership
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                return BridgeOperationResult.Failure(roleId, permission, "Role not found or not owned by tenant");
            }

            // 2. Validate permission is available to tenant
            var isAvailable = await _permissionCatalogService.IsPermissionAvailableToTenantAsync(permission, tenantId, cancellationToken);
            if (!isAvailable)
            {
                return BridgeOperationResult.Failure(roleId, permission, "Permission not available to tenant subscription tier");
            }

            // 3. Check if permission is already assigned
            var existingClaim = role.Claims.FirstOrDefault(c => c.ClaimType == "permission" && c.ClaimValue == permission);
            if (existingClaim != null)
            {
                return BridgeOperationResult.Failure(roleId, permission, "Permission already assigned to role");
            }

            // 4. Validate assignment
            var validationResult = await ValidatePermissionAssignmentAsync(roleId, permission, tenantId, cancellationToken);

            // 5. Add permission claim
            var claim = new Claim("permission", permission);
            var result = await _roleManager.AddClaimAsync(role, claim);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BridgeOperationResult.Failure(roleId, permission, $"Failed to assign permission: {errors}");
            }

            // 6. Update role metrics
            var currentPermissions = role.Claims.Count(c => c.ClaimType == "permission");
            
            // 7. Create successful result
            var operationResult = BridgeOperationResult.CreateSuccess(roleId, permission, assignedBy);
            operationResult.ValidationResults = validationResult;
            operationResult.RolePermissionCount = currentPermissions + 1;
            operationResult.AffectedUsers = role.UserCount;

            _logger.LogInformation("Assigned permission {Permission} to role {RoleId} for tenant {TenantId} by user {UserId}",
                permission, roleId, tenantId, assignedBy);

            ClearRoleCache(roleId, tenantId);
            return operationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permission {Permission} to role {RoleId} for tenant {TenantId}",
                permission, roleId, tenantId);
            return BridgeOperationResult.Failure(roleId, permission, $"Internal error: {ex.Message}");
        }
    }

    public async Task<BridgeOperationResult> RemovePermissionAsync(
        string roleId,
        string permission,
        string tenantId,
        string removedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate role ownership
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                return BridgeOperationResult.Failure(roleId, permission, "Role not found or not owned by tenant");
            }

            // 2. Check if permission is assigned
            var existingClaim = role.Claims.FirstOrDefault(c => c.ClaimType == "permission" && c.ClaimValue == permission);
            if (existingClaim == null)
            {
                return BridgeOperationResult.Failure(roleId, permission, "Permission not assigned to role");
            }

            // 3. Remove permission claim
            var claim = new Claim("permission", permission);
            var result = await _roleManager.RemoveClaimAsync(role, claim);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BridgeOperationResult.Failure(roleId, permission, $"Failed to remove permission: {errors}");
            }

            // 4. Update role metrics
            var currentPermissions = role.Claims.Count(c => c.ClaimType == "permission");
            
            // 5. Create successful result
            var operationResult = BridgeOperationResult.CreateSuccess(roleId, permission, removedBy);
            operationResult.RolePermissionCount = currentPermissions - 1;
            operationResult.AffectedUsers = role.UserCount;

            _logger.LogInformation("Removed permission {Permission} from role {RoleId} for tenant {TenantId} by user {UserId}",
                permission, roleId, tenantId, removedBy);

            ClearRoleCache(roleId, tenantId);
            return operationResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {Permission} from role {RoleId} for tenant {TenantId}",
                permission, roleId, tenantId);
            return BridgeOperationResult.Failure(roleId, permission, $"Internal error: {ex.Message}");
        }
    }

    public async Task<BulkBridgeOperationResult> BulkAssignPermissionsAsync(
        string roleId,
        BulkPermissionAssignmentRequest request,
        string tenantId,
        string assignedBy,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PermissionAssignmentResult>();
        var successCount = 0;

        try
        {
            // 1. Validate role ownership
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
            }

            // 2. Get tenant available permissions for validation
            var availablePermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(tenantId, cancellationToken);
            var availablePermissionIds = availablePermissions.Select(p => p.Id).ToHashSet();

            // 3. Process each permission
            foreach (var permission in request.Permissions)
            {
                var assignmentResult = new PermissionAssignmentResult
                {
                    Permission = permission
                };

                try
                {
                    // Check if permission is available to tenant
                    if (!availablePermissionIds.Contains(permission))
                    {
                        assignmentResult.Added = false;
                        assignmentResult.Reason = "Permission not available to tenant subscription tier";
                    }
                    else
                    {
                        // Check if already assigned
                        var existingClaim = role.Claims.FirstOrDefault(c => c.ClaimType == "permission" && c.ClaimValue == permission);
                        if (existingClaim != null)
                        {
                            assignmentResult.Added = false;
                            assignmentResult.Reason = "Permission already assigned to role";
                        }
                        else
                        {
                            // Add permission claim
                            var claim = new Claim("permission", permission);
                            var result = await _roleManager.AddClaimAsync(role, claim);
                            
                            if (result.Succeeded)
                            {
                                assignmentResult.Added = true;
                                assignmentResult.Reason = "Successfully assigned";
                                successCount++;
                            }
                            else
                            {
                                assignmentResult.Added = false;
                                assignmentResult.Reason = string.Join(", ", result.Errors.Select(e => e.Description));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    assignmentResult.Added = false;
                    assignmentResult.Reason = $"Error: {ex.Message}";
                    _logger.LogError(ex, "Error adding permission {Permission} to role {RoleId} in bulk operation", permission, roleId);
                }

                results.Add(assignmentResult);
            }

            // 4. Perform compliance validation on the role
            var validationResults = await _roleCompositionService.ValidateRoleComplianceAsync(roleId, tenantId, cancellationToken);

            // 5. Create bulk result
            var bulkResult = BulkBridgeOperationResult.BulkSuccess(results.ToArray());
            bulkResult.RoleId = roleId;
            bulkResult.PerformedBy = assignedBy;
            bulkResult.NewPermissionCount = role.Claims.Count(c => c.ClaimType == "permission");
            bulkResult.Source = request.Source;
            bulkResult.TemplateId = request.TemplateId;
            bulkResult.ValidationResults = new PermissionValidationResult
            {
                ComplianceCheck = validationResults.IsCompliant ? "passed" : "warning",
                Warnings = validationResults.Warnings.Select(w => w.Message).ToArray(),
                Recommendations = validationResults.Recommendations.Select(r => r.Reason).ToArray()
            };

            if (successCount > 0)
            {
                _logger.LogInformation("Bulk assigned {SuccessCount}/{TotalCount} permissions to role {RoleId} for tenant {TenantId}",
                    successCount, request.Permissions.Length, roleId, tenantId);
                ClearRoleCache(roleId, tenantId);
            }

            return bulkResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk permission assignment for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            throw;
        }
    }

    public async Task<BulkBridgeOperationResult> ReplacePermissionsAsync(
        string roleId,
        ReplacePermissionsRequest request,
        string tenantId,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate role ownership
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
            }

            // 2. Remove all existing permission claims
            var existingPermissionClaims = role.Claims.Where(c => c.ClaimType == "permission").ToList();
            foreach (var claim in existingPermissionClaims)
            {
                await _roleManager.RemoveClaimAsync(role, new Claim("permission", claim.ClaimValue!));
            }

            // 3. Add new permissions using bulk assign
            var bulkRequest = new BulkPermissionAssignmentRequest
            {
                Permissions = request.Permissions,
                Source = AssignmentSource.Bulk,
                Notes = request.Notes ?? "Permission replacement operation"
            };

            var result = await BulkAssignPermissionsAsync(roleId, bulkRequest, tenantId, updatedBy, cancellationToken);

            _logger.LogInformation("Replaced permissions for role {RoleId} for tenant {TenantId} by user {UserId} - {NewCount} permissions",
                roleId, tenantId, updatedBy, request.Permissions.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing permissions for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            throw;
        }
    }

    public async Task<AvailablePermission[]> GetAvailablePermissionsAsync(
        string roleId,
        string tenantId,
        string? category = null,
        bool excludeHighRisk = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"available_permissions:{roleId}:{tenantId}:{category}:{excludeHighRisk}";
        if (_cache.TryGetValue(cacheKey, out AvailablePermission[]? cachedPermissions))
        {
            return cachedPermissions!;
        }

        try
        {
            // 1. Get role and current permissions
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                return Array.Empty<AvailablePermission>();
            }

            var currentPermissions = role.Claims
                .Where(c => c.ClaimType == "permission")
                .Select(c => c.ClaimValue!)
                .ToHashSet();

            // 2. Get all tenant available permissions
            var allAvailablePermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(tenantId, cancellationToken);

            // 3. Filter out already assigned permissions
            var unassignedPermissions = allAvailablePermissions
                .Where(p => !currentPermissions.Contains(p.Id))
                .ToArray();

            // 4. Apply filters
            if (!string.IsNullOrWhiteSpace(category))
            {
                unassignedPermissions = unassignedPermissions
                    .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (excludeHighRisk)
            {
                unassignedPermissions = unassignedPermissions
                    .Where(p => p.RiskLevel < PermissionRiskLevel.High)
                    .ToArray();
            }

            // 5. Convert to available permission format with recommendations
            var availablePermissions = unassignedPermissions.Select(p => new AvailablePermission
            {
                Permission = p.Id,
                Name = p.Name,
                Description = p.Description,
                Category = p.Category,
                RiskLevel = p.RiskLevel.ToString().ToLower(),
                RequiresUpgrade = false, // Already filtered by tenant availability
                RecommendationScore = CalculateRecommendationScore(p, role),
                IsCommonlyUsed = IsCommonlyUsedPermission(p.Id)
            }).OrderByDescending(p => p.RecommendationScore).ToArray();

            // Cache for 5 minutes
            _cache.Set(cacheKey, availablePermissions, TimeSpan.FromMinutes(5));

            return availablePermissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available permissions for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return Array.Empty<AvailablePermission>();
        }
    }

    public async Task<ApplicationRole[]> GetRolesWithPermissionAsync(
        string permission,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _roleManager.Roles
                .Include(r => r.Claims)
                .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive &&
                           r.Claims.Any(c => c.ClaimType == "permission" && c.ClaimValue == permission))
                .OrderBy(r => r.Name)
                .ToArrayAsync(cancellationToken);

            return roles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles with permission {Permission} for tenant {TenantId}", permission, tenantId);
            return Array.Empty<ApplicationRole>();
        }
    }

    public async Task<TenantPermissionUsage[]> GetPermissionUsageAnalyticsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"permission_usage:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out TenantPermissionUsage[]? cachedUsage))
        {
            return cachedUsage!;
        }

        try
        {
            var roles = await _roleManager.Roles
                .Include(r => r.Claims)
                .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive)
                .ToArrayAsync(cancellationToken);

            var totalRoles = roles.Length;
            var totalUsers = roles.Sum(r => r.UserCount);

            // Group by permission and calculate usage
            var permissionUsage = roles
                .SelectMany(r => r.Claims.Where(c => c.ClaimType == "permission").Select(c => new { r, Permission = c.ClaimValue! }))
                .GroupBy(x => x.Permission)
                .Select(g => new TenantPermissionUsage
                {
                    Permission = g.Key,
                    Name = GetPermissionName(g.Key),
                    RoleCount = g.Count(),
                    UserCount = g.Sum(x => x.r.UserCount),
                    Coverage = totalRoles > 0 ? (double)g.Count() / totalRoles * 100 : 0,
                    FirstAssigned = g.Min(x => x.r.CreatedAt),
                    Category = GetPermissionCategory(g.Key)
                })
                .OrderByDescending(p => p.Coverage)
                .ToArray();

            // Cache for 10 minutes
            _cache.Set(cacheKey, permissionUsage, TimeSpan.FromMinutes(10));

            return permissionUsage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permission usage analytics for tenant {TenantId}", tenantId);
            return Array.Empty<TenantPermissionUsage>();
        }
    }

    public async Task<PermissionImpactAnalysis> AnalyzePermissionImpactAsync(
        string[] permissions,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _roleManager.Roles
                .Include(r => r.Claims)
                .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive)
                .ToArrayAsync(cancellationToken);

            var affectedRoles = roles
                .Where(r => permissions.Any(p => r.Claims.Any(c => c.ClaimType == "permission" && c.ClaimValue == p)))
                .ToArray();

            var affectedUsers = affectedRoles.Sum(r => r.UserCount);

            var analysis = new PermissionImpactAnalysis
            {
                AffectedTenants = 1, // Current tenant
                AffectedRoles = affectedRoles.Length,
                AffectedUsers = affectedUsers,
                PotentialIssues = AnalyzePotentialIssues(permissions, affectedRoles),
                Recommendations = GenerateImpactRecommendations(permissions, affectedRoles)
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing permission impact for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<RolePermissionMatrixResponse> GetRolePermissionMatrixAsync(
        string tenantId,
        string[]? permissionFilter = null,
        RoleCategory? roleCategory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _roleManager.Roles
                .Include(r => r.Claims)
                .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive)
                .ToArrayAsync(cancellationToken);

            if (roleCategory.HasValue)
            {
                roles = roles.Where(r => r.Category == roleCategory.Value).ToArray();
            }

            // Get all permissions to include in matrix
            var allPermissions = permissionFilter?.ToArray() ?? 
                await GetAllTenantPermissionsAsync(tenantId, cancellationToken);

            // Build matrix
            var matrix = roles.Select(role => new RolePermissionMatrix
            {
                Role = new RoleMatrixInfo
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    Category = role.Category
                },
                Permissions = allPermissions.Select(permission => new PermissionMatrixEntry
                {
                    Permission = permission,
                    HasPermission = role.Claims.Any(c => c.ClaimType == "permission" && c.ClaimValue == permission),
                    AssignedAt = role.Claims.FirstOrDefault(c => c.ClaimType == "permission" && c.ClaimValue == permission) != null ? role.CreatedAt : null,
                    Source = AssignmentSource.Manual // This would be tracked in a real implementation
                }).ToArray(),
                PermissionCount = role.Claims.Count(c => c.ClaimType == "permission"),
                UserCount = role.UserCount
            }).ToArray();

            // Calculate permission summaries
            var permissionSummaries = allPermissions.Select(permission =>
            {
                var rolesWithPermission = roles.Where(r => r.Claims.Any(c => c.ClaimType == "permission" && c.ClaimValue == permission)).ToArray();
                return new TenantPermissionUsage
                {
                    Permission = permission,
                    Name = GetPermissionName(permission),
                    RoleCount = rolesWithPermission.Length,
                    UserCount = rolesWithPermission.Sum(r => r.UserCount),
                    Coverage = roles.Length > 0 ? (double)rolesWithPermission.Length / roles.Length * 100 : 0,
                    Category = GetPermissionCategory(permission)
                };
            }).ToArray();

            var response = new RolePermissionMatrixResponse
            {
                Matrix = matrix,
                PermissionSummary = permissionSummaries,
                TenantStats = new PermissionMatrixSummary
                {
                    TotalRoles = roles.Length,
                    TotalPermissions = allPermissions.Length,
                    TotalUsers = roles.Sum(r => r.UserCount),
                    AveragePermissionsPerRole = roles.Length > 0 ? roles.Average(r => r.Claims.Count(c => c.ClaimType == "permission")) : 0,
                    TopPermissions = permissionSummaries.OrderByDescending(p => p.Coverage).Take(10).ToArray()
                },
                Filters = new MatrixFilterInfo
                {
                    PermissionFilter = permissionFilter ?? Array.Empty<string>(),
                    RoleCategory = roleCategory,
                    ExcludeInactive = true
                }
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role-permission matrix for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<PermissionAssignment[]> GetRoleAssignmentHistoryAsync(
        string roleId,
        string tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        // In a real implementation, this would query the PermissionAssignment table
        // For now, return basic assignment info based on current role claims
        try
        {
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                return Array.Empty<PermissionAssignment>();
            }

            var assignments = role.Claims
                .Where(c => c.ClaimType == "permission")
                .Select(c => new PermissionAssignment
                {
                    RoleId = roleId,
                    Permission = c.ClaimValue!,
                    AssignedAt = role.CreatedAt, // Would be actual assignment date in real implementation
                    AssignedBy = role.CreatedBy,
                    Source = AssignmentSource.Manual,
                    IsActive = true,
                    Role = role
                })
                .ToArray();

            if (fromDate.HasValue)
            {
                assignments = assignments.Where(a => a.AssignedAt >= fromDate.Value).ToArray();
            }

            if (toDate.HasValue)
            {
                assignments = assignments.Where(a => a.AssignedAt <= toDate.Value).ToArray();
            }

            return assignments.OrderByDescending(a => a.AssignedAt).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignment history for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return Array.Empty<PermissionAssignment>();
        }
    }

    public async Task<PermissionChangeEntry[]> GetRecentPermissionChangesAsync(
        string tenantId,
        int maxResults = 50,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // In a real implementation, this would query audit logs
            // For now, return recent role creations as permission changes
            var roles = await _roleManager.Roles
                .Include(r => r.Claims)
                .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive)
                .OrderByDescending(r => r.CreatedAt)
                .Take(maxResults)
                .ToArrayAsync(cancellationToken);

            var changes = new List<PermissionChangeEntry>();

            foreach (var role in roles)
            {
                if (fromDate.HasValue && role.CreatedAt < fromDate.Value)
                    continue;

                var permissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
                foreach (var permission in permissions)
                {
                    changes.Add(new PermissionChangeEntry
                    {
                        ChangeType = "assigned",
                        RoleId = role.Id,
                        RoleName = role.Name,
                        Permission = permission,
                        PermissionName = GetPermissionName(permission),
                        ChangedAt = role.CreatedAt,
                        ChangedBy = role.CreatedBy,
                        Source = AssignmentSource.Manual,
                        AffectedUsers = role.UserCount
                    });
                }
            }

            return changes.OrderByDescending(c => c.ChangedAt).Take(maxResults).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent permission changes for tenant {TenantId}", tenantId);
            return Array.Empty<PermissionChangeEntry>();
        }
    }

    public async Task<PermissionValidationResult> ValidatePermissionAssignmentAsync(
        string roleId,
        string permission,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new PermissionValidationResult();
            var warnings = new List<string>();
            var conflicts = new List<string>();
            var recommendations = new List<string>();

            // 1. Get role and permission info
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                result.ComplianceCheck = "failed";
                conflicts.Add("Role not found or not owned by tenant");
                result.Conflicts = conflicts.ToArray();
                return result;
            }

            var allPermissions = await _permissionCatalogService.GetAllPermissionsAsync(cancellationToken: cancellationToken);
            var permissionInfo = allPermissions.FirstOrDefault(p => p.Id == permission);
            
            if (permissionInfo == null)
            {
                result.ComplianceCheck = "failed";
                conflicts.Add("Permission does not exist");
                result.Conflicts = conflicts.ToArray();
                return result;
            }

            // 2. Check if permission is available to tenant
            var isAvailable = await _permissionCatalogService.IsPermissionAvailableToTenantAsync(permission, tenantId, cancellationToken);
            if (!isAvailable)
            {
                result.ComplianceCheck = "failed";
                conflicts.Add("Permission not available to tenant subscription tier");
                result.Conflicts = conflicts.ToArray();
                return result;
            }

            // 3. Check risk level
            if (permissionInfo.RiskLevel >= PermissionRiskLevel.High)
            {
                warnings.Add("This is a high-risk permission - ensure appropriate user assignment");
            }

            // 4. Check for segregation conflicts
            var currentPermissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
            var segregationConflicts = CheckSegregationConflicts(currentPermissions.Append(permission).ToArray());
            conflicts.AddRange(segregationConflicts);

            // 5. Generate recommendations
            if (permissionInfo.Category == "Compliance" && !currentPermissions.Contains("audit_trail:view"))
            {
                recommendations.Add("Consider adding 'audit_trail:view' permission for compliance roles");
            }

            result.ComplianceCheck = conflicts.Count == 0 ? "passed" : "warning";
            result.Conflicts = conflicts.ToArray();
            result.Warnings = warnings.ToArray();
            result.Recommendations = recommendations.ToArray();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating permission assignment {Permission} for role {RoleId}", permission, roleId);
            return new PermissionValidationResult
            {
                ComplianceCheck = "failed",
                Conflicts = new[] { $"Validation error: {ex.Message}" }
            };
        }
    }

    public async Task<string[]> GetRecommendedPermissionsForRoleAsync(
        string roleId,
        string tenantId,
        int maxRecommendations = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var role = await ValidateRoleOwnershipAsync(roleId, tenantId, cancellationToken);
            if (role == null)
            {
                return Array.Empty<string>();
            }

            var currentPermissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
            
            // Use the existing recommendation service
            var recommendations = await _permissionCatalogService.GetRecommendedPermissionsAsync(currentPermissions, tenantId, cancellationToken);
            
            return recommendations.Take(maxRecommendations).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommended permissions for role {RoleId} for tenant {TenantId}", roleId, tenantId);
            return Array.Empty<string>();
        }
    }

    public async Task<TenantPermissionHealthReport> AnalyzeTenantPermissionHealthAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _roleManager.Roles
                .Include(r => r.Claims)
                .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive)
                .ToArrayAsync(cancellationToken);

            var allAssignments = roles.SelectMany(r => r.Claims.Where(c => c.ClaimType == "permission")).ToArray();
            var totalAssignments = allAssignments.Length;

            // Calculate health scores
            var complianceScore = CalculateComplianceScore(roles);
            var segregationScore = CalculateSegregationScore(roles);
            var efficiencyScore = CalculateEfficiencyScore(roles);
            var securityScore = CalculateSecurityScore(roles);

            var overallScore = (int)((complianceScore + segregationScore + efficiencyScore + securityScore) / 4.0);

            var report = new TenantPermissionHealthReport
            {
                TenantId = tenantId,
                OverallHealthScore = overallScore,
                Assessment = new PermissionHealthAssessment
                {
                    ComplianceScore = complianceScore,
                    SegregationScore = segregationScore,
                    EfficiencyScore = efficiencyScore,
                    SecurityScore = securityScore,
                    TotalAssignments = totalAssignments,
                    RedundantAssignments = CalculateRedundantAssignments(roles),
                    HighRiskAssignments = CalculateHighRiskAssignments(roles),
                    OverPrivilegedRoleRate = CalculateOverPrivilegedRate(roles)
                },
                Optimizations = GenerateOptimizations(roles),
                ComplianceIssues = GenerateComplianceIssues(roles),
                UnusedPermissions = await FindUnusedPermissionsAsync(tenantId, cancellationToken),
                OverPrivilegedRoles = FindOverPrivilegedRoles(roles),
                Recommendations = GenerateHealthRecommendations(roles)
            };

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing tenant permission health for tenant {TenantId}", tenantId);
            throw;
        }
    }

    #region Private Helper Methods

    private async Task<ApplicationRole?> ValidateRoleOwnershipAsync(string roleId, string tenantId, CancellationToken cancellationToken)
    {
        return await _roleManager.Roles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == Guid.Parse(tenantId), cancellationToken);
    }

    private void ClearRoleCache(string roleId, string tenantId)
    {
        var cachePatterns = new[]
        {
            $"available_permissions:{roleId}:{tenantId}:",
            $"permission_usage:{tenantId}",
            $"tenant_roles:{tenantId}:"
        };

        // In a real implementation, you'd use a more sophisticated cache invalidation strategy
    }

    private int CalculateRecommendationScore(SystemPermission permission, ApplicationRole role)
    {
        var score = 50; // Base score

        // Boost score for category match
        if (permission.Category.Contains(role.Category.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        // Reduce score for high risk
        if (permission.RiskLevel >= PermissionRiskLevel.High)
        {
            score -= 15;
        }

        // Boost for commonly used permissions
        if (IsCommonlyUsedPermission(permission.Id))
        {
            score += 10;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private bool IsCommonlyUsedPermission(string permission)
    {
        // In a real implementation, this would query usage statistics
        var commonPermissions = new[] { "clients:view", "loans:create", "reports:view", "audit_trail:create" };
        return commonPermissions.Contains(permission);
    }

    private string GetPermissionName(string permission)
    {
        // Convert permission ID to human-readable name
        var parts = permission.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0].Replace("_", " ");
            var action = parts[1].Replace("_", " ");
            return $"{char.ToUpper(action[0])}{action[1..]} {char.ToUpper(resource[0])}{resource[1..]}";
        }
        return permission;
    }

    private string GetPermissionCategory(string permission)
    {
        // Extract category from permission ID
        var parts = permission.Split(':');
        if (parts.Length >= 1)
        {
            return parts[0] switch
            {
                "clients" => "Client Management",
                "loans" => "Loan Management",
                "reports" => "Reporting",
                "audit_trail" => "Audit & Compliance",
                "system" => "System Administration",
                _ => "General"
            };
        }
        return "General";
    }

    private async Task<string[]> GetAllTenantPermissionsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var availablePermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(tenantId, cancellationToken);
        return availablePermissions.Select(p => p.Id).ToArray();
    }

    private string[] CheckSegregationConflicts(string[] permissions)
    {
        var conflicts = new List<string>();

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
                var foundConflicts = conflictingPermissions.Intersect(permissions);
                conflicts.AddRange(foundConflicts.Select(c => $"Permission '{permission}' conflicts with '{c}' (segregation of duties)"));
            }
        }

        return conflicts.ToArray();
    }

    private string[] AnalyzePotentialIssues(string[] permissions, ApplicationRole[] affectedRoles)
    {
        var issues = new List<string>();

        // Check for high-risk permissions
        var highRiskCount = permissions.Count(p => GetPermissionRiskLevel(p) >= PermissionRiskLevel.High);
        if (highRiskCount > 0)
        {
            issues.Add($"{highRiskCount} high-risk permissions may require additional oversight");
        }

        // Check for segregation conflicts
        foreach (var role in affectedRoles)
        {
            var rolePermissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
            var conflicts = CheckSegregationConflicts(rolePermissions);
            if (conflicts.Length > 0)
            {
                issues.Add($"Role '{role.Name}' has segregation of duties conflicts");
            }
        }

        return issues.ToArray();
    }

    private string[] GenerateImpactRecommendations(string[] permissions, ApplicationRole[] affectedRoles)
    {
        var recommendations = new List<string>();

        if (affectedRoles.Length > 5)
        {
            recommendations.Add("Consider implementing changes in phases due to wide impact");
        }

        if (permissions.Any(p => GetPermissionRiskLevel(p) >= PermissionRiskLevel.High))
        {
            recommendations.Add("Additional approval may be required for high-risk permission changes");
        }

        return recommendations.ToArray();
    }

    private PermissionRiskLevel GetPermissionRiskLevel(string permission)
    {
        // In a real implementation, this would look up from the permission catalog
        if (permission.Contains("delete") || permission.Contains("system") || permission.Contains("emergency"))
            return PermissionRiskLevel.Critical;
        if (permission.Contains("approve") || permission.Contains("disburse") || permission.Contains("gl"))
            return PermissionRiskLevel.High;
        if (permission.Contains("edit") || permission.Contains("create"))
            return PermissionRiskLevel.Medium;
        return PermissionRiskLevel.Low;
    }

    private int CalculateComplianceScore(ApplicationRole[] roles)
    {
        // Simplified compliance scoring
        var compliantRoles = roles.Count(r => r.Claims.Any(c => c.ClaimValue == "audit_trail:create"));
        return roles.Length > 0 ? (compliantRoles * 100) / roles.Length : 100;
    }

    private int CalculateSegregationScore(ApplicationRole[] roles)
    {
        var violationCount = 0;
        foreach (var role in roles)
        {
            var permissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
            var conflicts = CheckSegregationConflicts(permissions);
            if (conflicts.Length > 0) violationCount++;
        }
        return roles.Length > 0 ? Math.Max(0, 100 - ((violationCount * 100) / roles.Length)) : 100;
    }

    private int CalculateEfficiencyScore(ApplicationRole[] roles)
    {
        // Based on permission overlap and redundancy
        var avgPermissions = roles.Length > 0 ? roles.Average(r => r.Claims.Count(c => c.ClaimType == "permission")) : 0;
        return avgPermissions < 20 ? 100 : Math.Max(50, 100 - (int)((avgPermissions - 20) * 2));
    }

    private int CalculateSecurityScore(ApplicationRole[] roles)
    {
        var highRiskAssignments = CalculateHighRiskAssignments(roles);
        var totalAssignments = roles.Sum(r => r.Claims.Count(c => c.ClaimType == "permission"));
        if (totalAssignments == 0) return 100;
        
        var riskRatio = (double)highRiskAssignments / totalAssignments;
        return Math.Max(0, 100 - (int)(riskRatio * 200)); // Penalize high-risk ratios
    }

    private int CalculateRedundantAssignments(ApplicationRole[] roles)
    {
        // Simplified calculation - count duplicate permissions across similar roles
        return 0; // Would be implemented based on actual redundancy detection logic
    }

    private int CalculateHighRiskAssignments(ApplicationRole[] roles)
    {
        return roles.Sum(r => r.Claims.Count(c => c.ClaimType == "permission" && GetPermissionRiskLevel(c.ClaimValue!) >= PermissionRiskLevel.High));
    }

    private double CalculateOverPrivilegedRate(ApplicationRole[] roles)
    {
        // Simplified - roles with >15 permissions considered over-privileged
        var overPrivileged = roles.Count(r => r.Claims.Count(c => c.ClaimType == "permission") > 15);
        return roles.Length > 0 ? (double)overPrivileged / roles.Length * 100 : 0;
    }

    private PermissionOptimization[] GenerateOptimizations(ApplicationRole[] roles)
    {
        var optimizations = new List<PermissionOptimization>();

        // Find roles with too many permissions
        var overPrivilegedRoles = roles.Where(r => r.Claims.Count(c => c.ClaimType == "permission") > 15).ToArray();
        if (overPrivilegedRoles.Length > 0)
        {
            optimizations.Add(new PermissionOptimization
            {
                OptimizationType = "reduce_permissions",
                Description = "Some roles have excessive permissions that could be consolidated",
                Impact = "medium",
                AffectedRoles = overPrivilegedRoles.Select(r => r.Name).ToArray(),
                Effort = "low",
                ExpectedBenefit = "Improved security posture and easier role management"
            });
        }

        return optimizations.ToArray();
    }

    private PermissionComplianceIssue[] GenerateComplianceIssues(ApplicationRole[] roles)
    {
        var issues = new List<PermissionComplianceIssue>();

        foreach (var role in roles)
        {
            var permissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
            
            // Check for missing audit permissions
            if (role.Category == RoleCategory.Finance && !permissions.Contains("audit_trail:create"))
            {
                issues.Add(new PermissionComplianceIssue
                {
                    Severity = IssueSeverity.Medium,
                    IssueType = "missing_audit_permission",
                    Description = "Finance role lacks required audit trail permission",
                    RoleId = role.Id,
                    RoleName = role.Name,
                    RecommendedAction = "Add 'audit_trail:create' permission",
                    Framework = ComplianceFramework.BoZ
                });
            }
        }

        return issues.ToArray();
    }

    private async Task<string[]> FindUnusedPermissionsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var availablePermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(tenantId, cancellationToken);
        var usedPermissions = await GetPermissionUsageAnalyticsAsync(tenantId, cancellationToken);
        
        var usedPermissionIds = usedPermissions.Select(u => u.Permission).ToHashSet();
        
        return availablePermissions
            .Where(p => !usedPermissionIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToArray();
    }

    private string[] FindOverPrivilegedRoles(ApplicationRole[] roles)
    {
        return roles
            .Where(r => r.Claims.Count(c => c.ClaimType == "permission") > 15)
            .Select(r => r.Name)
            .ToArray();
    }

    private string[] GenerateHealthRecommendations(ApplicationRole[] roles)
    {
        var recommendations = new List<string>();

        var overPrivilegedCount = roles.Count(r => r.Claims.Count(c => c.ClaimType == "permission") > 15);
        if (overPrivilegedCount > 0)
        {
            recommendations.Add($"Review {overPrivilegedCount} over-privileged roles for permission optimization");
        }

        var rolesWithoutAudit = roles.Count(r => !r.Claims.Any(c => c.ClaimValue == "audit_trail:create"));
        if (rolesWithoutAudit > 0)
        {
            recommendations.Add("Consider adding audit trail permissions to improve compliance");
        }

        return recommendations.ToArray();
    }

    #endregion
}