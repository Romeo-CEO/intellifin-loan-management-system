using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Constants;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Implementation of tenant role composition and permission assignment service
/// Handles the "Lego brick" role building system with compliance validation
/// </summary>
public class RoleCompositionService : IRoleCompositionService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPermissionCatalogService _permissionCatalogService;
    private readonly ITenantResolver _tenantResolver;
    private readonly IRoleTemplateService _roleTemplateService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RoleCompositionService> _logger;

    public RoleCompositionService(
        RoleManager<ApplicationRole> roleManager,
        IPermissionCatalogService permissionCatalogService,
        ITenantResolver tenantResolver,
        IRoleTemplateService roleTemplateService,
        IMemoryCache cache,
        ILogger<RoleCompositionService> logger)
    {
        _roleManager = roleManager;
        _permissionCatalogService = permissionCatalogService;
        _tenantResolver = tenantResolver;
        _roleTemplateService = roleTemplateService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApplicationRole> CreateRoleAsync(
        CreateRoleRequest request,
        string tenantId,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Role name is required", nameof(request));
        }

        // Check if role name already exists for this tenant
        var existingRole = await _roleManager.Roles
            .FirstOrDefaultAsync(r => r.Name == request.Name && r.TenantId == Guid.Parse(tenantId), cancellationToken);

        if (existingRole != null)
        {
            throw new InvalidOperationException($"Role '{request.Name}' already exists for this tenant");
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            NormalizedName = request.Name.ToUpperInvariant(),
            Description = request.Description,
            TenantId = Guid.Parse(tenantId),
            Category = request.Category,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsCustom = string.IsNullOrWhiteSpace(request.TemplateId),
            TemplateId = request.TemplateId,
            IsActive = true
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create role: {errors}");
        }

        // Add initial permissions if provided
        if (request.InitialPermissions.Length > 0)
        {
            var permissionRequest = new AddPermissionsToRoleRequest
            {
                Permissions = request.InitialPermissions,
                Notes = "Initial permissions assigned during role creation"
            };

            await AddPermissionsToRoleAsync(role.Id, permissionRequest, tenantId, createdBy, cancellationToken);
        }

        _logger.LogInformation("Created role {RoleName} ({RoleId}) for tenant {TenantId} by user {UserId}", 
            role.Name, role.Id, tenantId, createdBy);

        return role;
    }

    public async Task<ApplicationRole> UpdateRoleAsync(
        string roleId,
        UpdateRoleRequest request,
        string tenantId,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
        }

        var updatesMade = false;

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != role.Name)
        {
            // Check if new name already exists
            var existingRole = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.Name == request.Name && r.TenantId == Guid.Parse(tenantId) && r.Id != roleId, 
                    cancellationToken);

            if (existingRole != null)
            {
                throw new InvalidOperationException($"Role '{request.Name}' already exists for this tenant");
            }

            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpperInvariant();
            updatesMade = true;
        }

        if (request.Description != null && request.Description != role.Description)
        {
            role.Description = request.Description;
            updatesMade = true;
        }

        if (request.Category.HasValue && request.Category.Value != role.Category)
        {
            role.Category = request.Category.Value;
            updatesMade = true;
        }

        if (request.IsActive.HasValue && request.IsActive.Value != role.IsActive)
        {
            role.IsActive = request.IsActive.Value;
            updatesMade = true;
        }

        if (updatesMade)
        {
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = updatedBy;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to update role: {errors}");
            }

            _logger.LogInformation("Updated role {RoleName} ({RoleId}) for tenant {TenantId} by user {UserId}", 
                role.Name, role.Id, tenantId, updatedBy);
        }

        return role;
    }

    public async Task<bool> DeleteRoleAsync(
        string roleId,
        string tenantId,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            return false;
        }

        // Check if role has users assigned
        if (role.UserCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it has {role.UserCount} users assigned");
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to delete role: {errors}");
        }

        _logger.LogInformation("Deleted role {RoleName} ({RoleId}) for tenant {TenantId} by user {UserId}", 
            role.Name, role.Id, tenantId, deletedBy);

        return true;
    }

    public async Task<ApplicationRole[]> GetTenantRolesAsync(
        string tenantId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"tenant_roles:{tenantId}:{includeInactive}";
        if (_cache.TryGetValue(cacheKey, out ApplicationRole[]? cachedRoles))
        {
            return cachedRoles!;
        }

        var query = _roleManager.Roles
            .Where(r => r.TenantId == Guid.Parse(tenantId));

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        var roles = await query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToArrayAsync(cancellationToken);

        // Cache for 5 minutes
        _cache.Set(cacheKey, roles, TimeSpan.FromMinutes(5));

        return roles;
    }

    public async Task<ApplicationRole?> GetRoleByIdAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _roleManager.Roles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == Guid.Parse(tenantId), cancellationToken);
    }

    public async Task<RolePermissionResult> AddPermissionsToRoleAsync(
        string roleId,
        AddPermissionsToRoleRequest request,
        string tenantId,
        string assignedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
        }

        var result = new RolePermissionResult
        {
            RoleId = roleId,
            AddedPermissions = new PermissionAssignmentResult[request.Permissions.Length]
        };

        var successCount = 0;
        var tenantAvailablePermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(tenantId, cancellationToken);
        var availablePermissionIds = tenantAvailablePermissions.Select(p => p.Id).ToHashSet();

        for (int i = 0; i < request.Permissions.Length; i++)
        {
            var permission = request.Permissions[i];
            var assignmentResult = new PermissionAssignmentResult
            {
                Permission = permission
            };

            try
            {
                // Validate permission exists and is available to tenant
                if (!availablePermissionIds.Contains(permission))
                {
                    assignmentResult.Added = false;
                    assignmentResult.Reason = "Permission not available to tenant subscription tier";
                }
                else
                {
                    // Check if permission is already assigned
                    var existingClaim = role.Claims.FirstOrDefault(c => c.ClaimType == "permission" && c.ClaimValue == permission);
                    if (existingClaim != null)
                    {
                        assignmentResult.Added = false;
                        assignmentResult.Reason = "Permission already assigned to role";
                    }
                    else
                    {
                        // Add permission as claim
                        var addResult = await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim("permission", permission));
                        if (addResult.Succeeded)
                        {
                            assignmentResult.Added = true;
                            assignmentResult.Reason = "Successfully added";
                            successCount++;
                        }
                        else
                        {
                            assignmentResult.Added = false;
                            assignmentResult.Reason = string.Join(", ", addResult.Errors.Select(e => e.Description));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                assignmentResult.Added = false;
                assignmentResult.Reason = $"Error: {ex.Message}";
                _logger.LogError(ex, "Error adding permission {Permission} to role {RoleId}", permission, roleId);
            }

            result.AddedPermissions[i] = assignmentResult;
        }

        // Perform compliance validation
        result.ValidationResults = await ValidateRoleComplianceAsync(roleId, tenantId, cancellationToken);
        result.TotalPermissions = role.Claims.Count(c => c.ClaimType == "permission");
        result.IsSuccess = successCount > 0;

        if (successCount > 0)
        {
            _logger.LogInformation("Added {SuccessCount}/{TotalCount} permissions to role {RoleId} for tenant {TenantId}", 
                successCount, request.Permissions.Length, roleId, tenantId);
        }

        return result;
    }

    public async Task<bool> RemovePermissionFromRoleAsync(
        string roleId,
        string permissionId,
        string tenantId,
        string removedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            return false;
        }

        var claim = role.Claims.FirstOrDefault(c => c.ClaimType == "permission" && c.ClaimValue == permissionId);
        if (claim == null)
        {
            return false;
        }

        var result = await _roleManager.RemoveClaimAsync(role, new System.Security.Claims.Claim("permission", permissionId));
        if (result.Succeeded)
        {
            _logger.LogInformation("Removed permission {Permission} from role {RoleId} for tenant {TenantId} by user {UserId}", 
                permissionId, roleId, tenantId, removedBy);
            return true;
        }

        return false;
    }

    public async Task<RolePermissionResult> BulkUpdateRolePermissionsAsync(
        string roleId,
        BulkUpdateRolePermissionsRequest request,
        string tenantId,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
        }

        // Remove all existing permission claims
        var existingPermissionClaims = role.Claims.Where(c => c.ClaimType == "permission").ToList();
        foreach (var claim in existingPermissionClaims)
        {
            await _roleManager.RemoveClaimAsync(role, new System.Security.Claims.Claim("permission", claim.ClaimValue!));
        }

        // Add new permissions
        var addRequest = new AddPermissionsToRoleRequest
        {
            Permissions = request.Permissions,
            Notes = request.Notes ?? "Bulk update of role permissions"
        };

        var result = await AddPermissionsToRoleAsync(roleId, addRequest, tenantId, updatedBy, cancellationToken);

        _logger.LogInformation("Bulk updated permissions for role {RoleId} for tenant {TenantId} by user {UserId}", 
            roleId, tenantId, updatedBy);

        return result;
    }

    public async Task<string[]> GetRolePermissionsAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            return Array.Empty<string>();
        }

        return role.Claims
            .Where(c => c.ClaimType == "permission")
            .Select(c => c.ClaimValue!)
            .OrderBy(p => p)
            .ToArray();
    }

    public async Task<ComplianceValidationResult> ValidateRoleComplianceAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
        }

        var result = new ComplianceValidationResult();
        var checks = new List<ComplianceCheck>();
        var issues = new List<ComplianceIssue>();
        var warnings = new List<ComplianceWarning>();
        var recommendations = new List<ComplianceRecommendation>();

        var rolePermissions = role.Claims.Where(c => c.ClaimType == "permission").Select(c => c.ClaimValue!).ToArray();
        var allPermissions = await _permissionCatalogService.GetAllPermissionsAsync(cancellationToken: cancellationToken);
        var permissionLookup = allPermissions.ToDictionary(p => p.Id, p => p);

        // 1. Segregation of duties check
        var segregationCheck = ValidateSegregationOfDuties(rolePermissions, permissionLookup);
        checks.Add(segregationCheck);

        // 2. BoZ requirements check
        var bozCheck = ValidateBozRequirements(rolePermissions, role.Category);
        checks.Add(bozCheck);

        // 3. Subscription tier check
        var tierCheck = await ValidateSubscriptionTierAsync(rolePermissions, tenantId, cancellationToken);
        checks.Add(tierCheck);

        // 4. Risk level assessment
        var riskCheck = ValidateRiskLevel(rolePermissions, permissionLookup);
        checks.Add(riskCheck);

        // Calculate overall score
        var totalWeight = checks.Sum(c => c.Weight);
        var weightedScore = checks.Sum(c => c.Score * c.Weight) / totalWeight;
        result.OverallScore = (int)Math.Round(weightedScore);
        result.IsCompliant = result.OverallScore >= 70 && !checks.Any(c => !c.Passed && c.Weight > 0.5);

        result.Checks = checks.ToArray();
        result.ComplianceIssues = issues.ToArray();
        result.Warnings = warnings.ToArray();
        result.Recommendations = recommendations.ToArray();

        return result;
    }

    public async Task<ComplianceValidationResult[]> CheckTenantRoleComplianceAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var roles = await GetTenantRolesAsync(tenantId, false, cancellationToken);
        var results = new List<ComplianceValidationResult>();

        foreach (var role in roles)
        {
            var validation = await ValidateRoleComplianceAsync(role.Id, tenantId, cancellationToken);
            results.Add(validation);
        }

        return results.ToArray();
    }

    public async Task<RolePreviewResult> PreviewRoleAsync(
        string roleId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleByIdAsync(roleId, tenantId, cancellationToken);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role {roleId} not found for tenant {tenantId}");
        }

        var permissions = await GetRolePermissionsAsync(roleId, tenantId, cancellationToken);
        var allPermissions = await _permissionCatalogService.GetAllPermissionsAsync(cancellationToken: cancellationToken);
        var permissionLookup = allPermissions.ToDictionary(p => p.Id, p => p);

        var capabilities = GenerateRoleCapabilities(permissions, permissionLookup);
        var riskAssessment = AssessRoleRisk(permissions, permissionLookup);
        var complianceResults = await ValidateRoleComplianceAsync(roleId, tenantId, cancellationToken);
        var similarRoles = await FindSimilarRolesAsync(roleId, tenantId, permissions, cancellationToken);

        return new RolePreviewResult
        {
            RoleId = roleId,
            RoleName = role.Name,
            Permissions = permissions,
            Capabilities = capabilities,
            ComplianceResults = complianceResults,
            RiskAssessment = riskAssessment,
            SimilarRoles = similarRoles
        };
    }

    public async Task<TenantRoleSummary> GetTenantRoleSummaryAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var roles = await GetTenantRolesAsync(tenantId, true, cancellationToken);
        var complianceResults = await CheckTenantRoleComplianceAsync(tenantId, cancellationToken);

        return new TenantRoleSummary
        {
            TotalRoles = roles.Length,
            ActiveRoles = roles.Count(r => r.IsActive),
            TotalUsers = roles.Sum(r => r.UserCount),
            ComplianceIssues = complianceResults.Sum(c => c.ComplianceIssues.Length),
            AverageComplianceScore = complianceResults.Length > 0 ? complianceResults.Average(c => c.OverallScore) : 0,
            LastRoleCreated = roles.Where(r => r.CreatedAt != default).Max(r => r.CreatedAt),
            LastRoleModified = roles.Where(r => r.UpdatedAt.HasValue).Max(r => r.UpdatedAt)
        };
    }

    public async Task<ApplicationRole> CreateRoleFromTemplateAsync(
        CreateRoleFromTemplateRequest request,
        string tenantId,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var template = await _roleTemplateService.GetTemplateByIdAsync(request.TemplateId, cancellationToken);
        if (template == null)
        {
            throw new KeyNotFoundException($"Role template {request.TemplateId} not found");
        }

        // Validate tenant can use this template
        var tenantTemplates = await _roleTemplateService.GetTemplatesForTenantAsync(tenantId, cancellationToken: cancellationToken);
        if (!tenantTemplates.Any(t => t.Id == request.TemplateId))
        {
            throw new InvalidOperationException("Template not available to tenant subscription tier");
        }

        var permissions = new List<string>(template.RequiredPermissions);

        if (request.IncludeAllRecommended)
        {
            permissions.AddRange(template.RecommendedPermissions);
        }

        if (request.PermissionModifications != null)
        {
            permissions.AddRange(request.PermissionModifications.AdditionalPermissions);
            permissions.RemoveAll(p => request.PermissionModifications.ExcludedPermissions.Contains(p) && 
                                     !template.RequiredPermissions.Contains(p)); // Cannot exclude required permissions
        }

        var createRequest = new CreateRoleRequest
        {
            Name = request.RoleName,
            Description = request.RoleDescription ?? template.Description,
            Category = template.Category,
            InitialPermissions = permissions.Distinct().ToArray(),
            TemplateId = request.TemplateId
        };

        var role = await CreateRoleAsync(createRequest, tenantId, createdBy, cancellationToken);

        // Update template usage statistics
        await _roleTemplateService.UpdateTemplateUsageAsync(request.TemplateId, tenantId, cancellationToken);

        return role;
    }

    public async Task UpdateRoleUserCountAsync(
        string roleId,
        int newCount,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role != null && role.UserCount != newCount)
        {
            role.UserCount = newCount;
            await _roleManager.UpdateAsync(role);
        }
    }

    public async Task<ApplicationRole[]> SearchTenantRolesAsync(
        string tenantId,
        string query,
        RoleCategory? category = null,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var roleQuery = _roleManager.Roles
            .Where(r => r.TenantId == Guid.Parse(tenantId) && r.IsActive);

        if (category.HasValue)
        {
            roleQuery = roleQuery.Where(r => r.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            roleQuery = roleQuery.Where(r => 
                r.Name.Contains(query) || 
                (r.Description != null && r.Description.Contains(query)));
        }

        return await roleQuery
            .Take(maxResults)
            .OrderBy(r => r.Name)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ApplicationRole[]> GetRolesByCategoryAsync(
        string tenantId,
        RoleCategory category,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _roleManager.Roles
            .Where(r => r.TenantId == Guid.Parse(tenantId) && r.Category == category);

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.Name)
            .ToArrayAsync(cancellationToken);
    }

    #region Private Helper Methods

    private ComplianceCheck ValidateSegregationOfDuties(string[] permissions, Dictionary<string, SystemPermission> permissionLookup)
    {
        var conflicts = new List<string>();

        // Define conflicting permission patterns
        var segregationRules = new Dictionary<string, string[]>
        {
            ["loans:approve"] = new[] { "loans:disburse", "gl:post" },
            ["loans:disburse"] = new[] { "loans:approve", "payments:record" },
            ["gl:post"] = new[] { "gl:reverse", "loans:approve" },
            ["system:users_manage"] = new[] { "audit_trail:view" },
        };

        foreach (var permission in permissions)
        {
            if (segregationRules.TryGetValue(permission, out var conflictingPermissions))
            {
                var foundConflicts = conflictingPermissions.Intersect(permissions);
                conflicts.AddRange(foundConflicts.Select(c => $"{permission} conflicts with {c}"));
            }
        }

        return new ComplianceCheck
        {
            CheckType = "segregation_of_duties",
            Passed = conflicts.Count == 0,
            Message = conflicts.Count == 0 ? "No conflicting permissions detected" : $"Found {conflicts.Count} segregation conflicts",
            Score = conflicts.Count == 0 ? 100 : Math.Max(0, 100 - (conflicts.Count * 25)),
            Weight = 1.0
        };
    }

    private ComplianceCheck ValidateBozRequirements(string[] permissions, RoleCategory category)
    {
        var requiredPermissions = GetBozRequiredPermissions(category);
        var missingPermissions = requiredPermissions.Except(permissions).ToArray();

        return new ComplianceCheck
        {
            CheckType = "boz_requirements",
            Passed = missingPermissions.Length == 0,
            Message = missingPermissions.Length == 0 ? 
                "All BoZ mandatory permissions present" : 
                $"Missing {missingPermissions.Length} mandatory permissions",
            Score = requiredPermissions.Length == 0 ? 100 : 
                Math.Max(0, 100 - ((missingPermissions.Length * 100) / requiredPermissions.Length)),
            Weight = 1.5
        };
    }

    private async Task<ComplianceCheck> ValidateSubscriptionTierAsync(string[] permissions, string tenantId, CancellationToken cancellationToken)
    {
        var availablePermissions = await _permissionCatalogService.GetTenantAvailablePermissionsAsync(tenantId, cancellationToken);
        var availablePermissionIds = availablePermissions.Select(p => p.Id).ToHashSet();
        var unavailablePermissions = permissions.Except(availablePermissionIds).ToArray();

        return new ComplianceCheck
        {
            CheckType = "subscription_tier",
            Passed = unavailablePermissions.Length == 0,
            Message = unavailablePermissions.Length == 0 ? 
                "All permissions available to current tier" : 
                $"{unavailablePermissions.Length} permissions not available to subscription tier",
            Score = permissions.Length == 0 ? 100 : 
                Math.Max(0, 100 - ((unavailablePermissions.Length * 100) / permissions.Length)),
            Weight = 1.0
        };
    }

    private ComplianceCheck ValidateRiskLevel(string[] permissions, Dictionary<string, SystemPermission> permissionLookup)
    {
        var highRiskCount = permissions.Count(p => 
            permissionLookup.TryGetValue(p, out var perm) && perm.RiskLevel >= PermissionRiskLevel.High);

        var riskScore = highRiskCount switch
        {
            0 => 100,
            1 => 90,
            2 => 80,
            3 => 70,
            _ => Math.Max(0, 70 - ((highRiskCount - 3) * 10))
        };

        return new ComplianceCheck
        {
            CheckType = "risk_level",
            Passed = highRiskCount <= 2,
            Message = highRiskCount == 0 ? 
                "No high-risk permissions" : 
                $"{highRiskCount} high-risk permissions detected",
            Score = riskScore,
            Weight = 0.8
        };
    }

    private string[] GetBozRequiredPermissions(RoleCategory category)
    {
        return category switch
        {
            RoleCategory.LoanOfficers => new[] { "audit_trail:create", "clients:view" },
            RoleCategory.CreditManagement => new[] { "audit_trail:create", "compliance:view" },
            RoleCategory.Finance => new[] { "audit_trail:create", "compliance:view", "gl:view" },
            RoleCategory.Compliance => new[] { "audit_trail:view", "compliance:manage" },
            RoleCategory.Management => new[] { "audit_trail:view", "compliance:view" },
            _ => Array.Empty<string>()
        };
    }

    private RoleCapability[] GenerateRoleCapabilities(string[] permissions, Dictionary<string, SystemPermission> permissionLookup)
    {
        var capabilities = new Dictionary<string, List<string>>();

        foreach (var permission in permissions)
        {
            if (permissionLookup.TryGetValue(permission, out var perm))
            {
                if (!capabilities.ContainsKey(perm.Category))
                {
                    capabilities[perm.Category] = new List<string>();
                }
                capabilities[perm.Category].Add($"{perm.Action} {perm.Resource}");
            }
        }

        return capabilities.Select(kvp => new RoleCapability
        {
            Area = kvp.Key,
            Actions = kvp.Value.ToArray(),
            RiskLevel = PermissionRiskLevel.Low // This would be calculated based on permissions
        }).ToArray();
    }

    private RoleRiskAssessment AssessRoleRisk(string[] permissions, Dictionary<string, SystemPermission> permissionLookup)
    {
        var riskScores = permissions
            .Where(p => permissionLookup.ContainsKey(p))
            .Select(p => (int)permissionLookup[p].RiskLevel)
            .ToArray();

        var averageRisk = riskScores.Length > 0 ? riskScores.Average() : 0;
        var overallScore = (int)(averageRisk * 25); // Convert to 0-100 scale

        var highRiskPermissions = permissions
            .Where(p => permissionLookup.TryGetValue(p, out var perm) && perm.RiskLevel >= PermissionRiskLevel.High)
            .ToArray();

        return new RoleRiskAssessment
        {
            OverallRiskScore = overallScore,
            RiskLevel = (PermissionRiskLevel)Math.Min(4, (int)Math.Ceiling(averageRisk)),
            HighRiskPermissions = highRiskPermissions,
            RiskFactors = GenerateRiskFactors(permissions, permissionLookup),
            Mitigations = GenerateMitigations(permissions, permissionLookup)
        };
    }

    private string[] GenerateRiskFactors(string[] permissions, Dictionary<string, SystemPermission> permissionLookup)
    {
        var factors = new List<string>();

        var highRiskCount = permissions.Count(p => 
            permissionLookup.TryGetValue(p, out var perm) && perm.RiskLevel >= PermissionRiskLevel.High);

        if (highRiskCount > 2)
        {
            factors.Add($"Multiple high-risk permissions ({highRiskCount})");
        }

        if (permissions.Contains("loans:approve") && permissions.Contains("loans:disburse"))
        {
            factors.Add("Combines loan approval and disbursement authority");
        }

        return factors.ToArray();
    }

    private string[] GenerateMitigations(string[] permissions, Dictionary<string, SystemPermission> permissionLookup)
    {
        var mitigations = new List<string>();

        var highRiskCount = permissions.Count(p => 
            permissionLookup.TryGetValue(p, out var perm) && perm.RiskLevel >= PermissionRiskLevel.High);

        if (highRiskCount > 0)
        {
            mitigations.Add("Implement additional approval workflows for high-risk operations");
            mitigations.Add("Enable enhanced audit logging for users with this role");
        }

        return mitigations.ToArray();
    }

    private async Task<SimilarRole[]> FindSimilarRolesAsync(
        string roleId, 
        string tenantId, 
        string[] permissions, 
        CancellationToken cancellationToken)
    {
        var allRoles = await GetTenantRolesAsync(tenantId, false, cancellationToken);
        var similarRoles = new List<SimilarRole>();

        foreach (var role in allRoles.Where(r => r.Id != roleId))
        {
            var rolePermissions = await GetRolePermissionsAsync(role.Id, tenantId, cancellationToken);
            var commonPermissions = permissions.Intersect(rolePermissions).Count();
            var uniquePermissions = rolePermissions.Except(permissions).Count();
            
            var totalPermissions = Math.Max(permissions.Length, rolePermissions.Length);
            var similarityScore = totalPermissions > 0 ? (commonPermissions * 100) / totalPermissions : 0;

            if (similarityScore > 30) // Only include roles with >30% similarity
            {
                similarRoles.Add(new SimilarRole
                {
                    RoleId = role.Id,
                    RoleName = role.Name,
                    SimilarityScore = similarityScore,
                    UserCount = role.UserCount,
                    CommonPermissions = commonPermissions,
                    UniquePermissions = uniquePermissions
                });
            }
        }

        return similarRoles.OrderByDescending(r => r.SimilarityScore).Take(5).ToArray();
    }

    #endregion
}