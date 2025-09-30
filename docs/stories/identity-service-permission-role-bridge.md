# Permission-Role Bridge Management - Multi-Tenant Enhancement

## Story Information
- **Story ID**: IDENTITY-007
- **Epic**: IntelliFin IdentityService API Development
- **Type**: Brownfield Multi-Tenant Enhancement
- **Priority**: High
- **Estimate**: 6-8 hours

## User Stories

### Primary Story - Permission Assignment Management
As a **Tenant Administrator** (Bank/MFI Admin),  
I want **intuitive APIs to assign and manage permissions on roles**,  
So that **I can easily build and maintain our organizational role structure**.

### Secondary Story - Role-Permission Analytics  
As a **Platform Administrator** (IntelliFin Team),  
I want **visibility into permission usage patterns across tenants**,  
So that **I can optimize the permission system and identify common patterns**.

## Architecture Context - The Bridge System

**Core Concept**: **Bridging Platform Permissions to Tenant Roles**
- **Bridge**: ASP.NET Identity's `IdentityRoleClaim` table connects roles to permissions
- **Atomic Operations**: Add/remove single permissions or bulk operations
- **Validation**: Ensure permission assignments comply with business rules
- **Analytics**: Track permission usage for optimization and compliance

**Bridge Principles:**
- Use built-in ASP.NET Identity infrastructure (`IdentityRoleClaim`)
- Provide both atomic and bulk permission assignment operations
- Validate all permission assignments against tenant constraints
- Track assignment history for audit and analytics
- Support complex permission inheritance and role combinations

## Acceptance Criteria

### **Tenant Plane API Requirements** (`/v1/*` endpoints)

**Permission Assignment:**
1. **POST /v1/roles/{roleId}/permissions/{permissionId}** - Assign single permission
2. **DELETE /v1/roles/{roleId}/permissions/{permissionId}** - Remove single permission
3. **POST /v1/roles/{roleId}/permissions/bulk** - Bulk assign permissions
4. **PUT /v1/roles/{roleId}/permissions/replace** - Replace all role permissions
5. **GET /v1/roles/{roleId}/permissions/available** - List unassigned permissions

**Permission Analysis:**
6. **GET /v1/permissions/{permissionId}/roles** - See which roles have permission
7. **GET /v1/permissions/usage** - Permission usage analytics for tenant
8. **POST /v1/permissions/impact-analysis** - Analyze impact of permission changes
9. **GET /v1/roles/permissions/matrix** - Role-permission matrix view

**Assignment History:**
10. **GET /v1/roles/{roleId}/permissions/history** - Permission assignment history
11. **GET /v1/permissions/changes** - Recent permission changes across all roles

**Tenant Context Requirements:**
12. All endpoints require JWT with `tenant_id` claim
13. Permission assignments automatically validated against tenant limits
14. Role-permission changes logged for compliance audit
15. Bulk operations support transaction rollback on validation failure

### **Platform Plane API Requirements** (`/platform/v1/*` endpoints)

**Cross-Tenant Analytics:**
16. **GET /platform/v1/permissions/analytics** - Permission usage across all tenants
17. **GET /platform/v1/roles/common-patterns** - Identify common role patterns
18. **GET /platform/v1/tenants/{tenantId}/permission-health** - Tenant permission health check
19. **POST /platform/v1/permissions/optimization-suggestions** - Suggest permission optimizations

**Platform Access Requirements:**
20. All endpoints require `PlatformAdmin` role
21. Cross-tenant analytics require explicit data aggregation permissions
22. Tenant-specific analysis requires tenant specification in URL

## Technical Architecture

### **Bridge Data Model (Using ASP.NET Identity)**
```csharp
// Built-in ASP.NET Identity table - we leverage this
public class IdentityRoleClaim<TKey>
{
    public int Id { get; set; }
    public TKey RoleId { get; set; }           // Links to ApplicationRole
    public string ClaimType { get; set; }     // Always "permission" for permissions
    public string ClaimValue { get; set; }    // The permission string (e.g., "loans:approve")
}

// Extension for enhanced tracking
public class PermissionAssignment
{
    public int Id { get; set; }
    public string RoleId { get; set; }
    public string Permission { get; set; }
    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; }
    public string? RemovedAt { get; set; }
    public string? RemovedBy { get; set; }
    public AssignmentSource Source { get; set; } // Manual, Template, Bulk
    public bool IsActive { get; set; } = true;
}

public enum AssignmentSource
{
    Manual,      // Individual assignment by admin
    Template,    // Applied from role template
    Bulk,        // Bulk operation
    Import,      // Data import
    Migration    // System migration
}
```

### **Bridge Service Implementation**
```csharp
public class PermissionRoleBridgeService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPermissionValidator _permissionValidator;
    private readonly IAuditService _auditService;
    
    public async Task<BridgeOperationResult> AssignPermissionAsync(
        string roleId,
        string permission, 
        string tenantId,
        string assignedBy)
    {
        // 1. Validate role belongs to tenant
        var role = await ValidateRoleOwnership(roleId, tenantId);
        
        // 2. Validate permission is available to tenant
        await _permissionValidator.ValidatePermissionAsync(permission, tenantId);
        
        // 3. Check for conflicts and compliance
        await ValidateAssignment(role, permission);
        
        // 4. Create permission claim
        var claim = new Claim("permission", permission);
        var result = await _roleManager.AddClaimAsync(role, claim);
        
        // 5. Log assignment for audit
        await _auditService.LogPermissionAssignmentAsync(
            roleId, permission, assignedBy, AssignmentSource.Manual);
            
        return BridgeOperationResult.Success();
    }
    
    public async Task<BridgeOperationResult> BulkAssignPermissionsAsync(
        string roleId,
        string[] permissions,
        string tenantId, 
        string assignedBy)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var results = new List<PermissionAssignmentResult>();
            
            foreach (var permission in permissions)
            {
                var result = await AssignPermissionAsync(roleId, permission, tenantId, assignedBy);
                results.Add(result);
            }
            
            await transaction.CommitAsync();
            return BridgeOperationResult.BulkSuccess(results);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

## API Design Preview

### **Tenant Plane APIs** (`/v1/*`)

#### POST /v1/roles/{roleId}/permissions/{permissionId} (Assign single permission)
```json
// Request (no body needed - permission specified in URL)

// Response
{
  "roleId": "role-123-456",
  "permission": "loans:approve",
  "assigned": true,
  "assignedAt": "2024-01-16T10:30:00Z",
  "assignedBy": "tenant-admin-789",
  "validationResults": {
    "complianceCheck": "passed",
    "conflicts": [],
    "warnings": [
      "This is a high-risk permission - ensure appropriate user assignment"
    ]
  },
  "rolePermissionCount": 13,
  "affectedUsers": 3
}
```

#### POST /v1/roles/{roleId}/permissions/bulk (Bulk assign permissions)
```json
{
  "permissions": [
    "clients:view",
    "clients:edit", 
    "loans:create",
    "loans:approve",
    "reports:view"
  ],
  "source": "template",
  "templateId": "senior-loan-officer"
}

// Response
{
  "roleId": "role-123-456",
  "roleName": "Senior Loan Officer", 
  "operation": "bulk_assign",
  "results": [
    {
      "permission": "clients:view",
      "assigned": true,
      "reason": "Successfully assigned"
    },
    {
      "permission": "clients:edit",
      "assigned": true, 
      "reason": "Successfully assigned"
    },
    {
      "permission": "loans:approve",
      "assigned": false,
      "reason": "Permission requires higher subscription tier"
    },
    {
      "permission": "reports:view",
      "assigned": true,
      "reason": "Successfully assigned" 
    }
  ],
  "summary": {
    "totalRequested": 5,
    "successfulAssignments": 4,
    "failedAssignments": 1,
    "newPermissionCount": 12
  },
  "validationResults": {
    "complianceStatus": "compliant",
    "segregationIssues": [],
    "recommendations": [
      "Consider adding 'audit-trail:view' for compliance"
    ]
  }
}
```

#### GET /v1/roles/{roleId}/permissions/available (List unassigned permissions)
```json
{
  "roleId": "role-123-456",
  "roleName": "Loan Officer",
  "currentPermissions": 8,
  "availablePermissions": [
    {
      "permission": "loans:approve",
      "name": "Approve Loans",
      "description": "Authority to approve loan applications",
      "category": "Loan Management",
      "riskLevel": "high",
      "requiresUpgrade": false,
      "recommendationScore": 85
    },
    {
      "permission": "reports:export", 
      "name": "Export Reports",
      "description": "Export reports to external formats",
      "category": "Reporting",
      "riskLevel": "medium",
      "requiresUpgrade": false,
      "recommendationScore": 60
    },
    {
      "permission": "system:advanced_config",
      "name": "Advanced System Configuration",
      "description": "Access to advanced system settings", 
      "category": "System Administration",
      "riskLevel": "critical",
      "requiresUpgrade": true,
      "upgradeRequired": "enterprise",
      "recommendationScore": 20
    }
  ],
  "recommendations": [
    {
      "permission": "loans:approve",
      "reason": "Commonly assigned to similar roles"
    }
  ],
  "filters": {
    "availableToTier": "professional",
    "excludeHighRisk": false,
    "category": "all"
  }
}
```

#### GET /v1/roles/permissions/matrix (Role-permission matrix)
```json
{
  "matrix": [
    {
      "roleId": "role-123-456",
      "roleName": "Loan Officer", 
      "permissions": [
        {"permission": "clients:view", "hasPermission": true},
        {"permission": "clients:edit", "hasPermission": true},
        {"permission": "loans:create", "hasPermission": true},
        {"permission": "loans:approve", "hasPermission": false},
        {"permission": "loans:disburse", "hasPermission": false}
      ],
      "permissionCount": 8,
      "userCount": 12
    },
    {
      "roleId": "role-789-012", 
      "roleName": "Credit Analyst",
      "permissions": [
        {"permission": "clients:view", "hasPermission": true},
        {"permission": "clients:edit", "hasPermission": false},
        {"permission": "loans:create", "hasPermission": false},
        {"permission": "loans:approve", "hasPermission": true},
        {"permission": "loans:disburse", "hasPermission": false}
      ],
      "permissionCount": 6,
      "userCount": 4
    }
  ],
  "permissionSummary": [
    {
      "permission": "clients:view",
      "name": "View Client Information",
      "roleCount": 6,
      "userCount": 28,
      "coverage": "87.5%"
    },
    {
      "permission": "loans:approve", 
      "name": "Approve Loans",
      "roleCount": 2,
      "userCount": 7,
      "coverage": "21.9%"
    }
  ],
  "tenantStats": {
    "totalRoles": 8,
    "totalPermissions": 45,
    "totalUsers": 32,
    "averagePermissionsPerRole": 11.2
  }
}
```

### **Platform Plane APIs** (`/platform/v1/*`)

#### GET /platform/v1/permissions/analytics (Cross-tenant permission analytics)
```json
{
  "permissionUsage": [
    {
      "permission": "clients:view",
      "name": "View Client Information",
      "tenantUsage": 15,
      "totalAssignments": 247,
      "averageRoleAssignments": 16.5,
      "category": "Client Management",
      "adoptionRate": "100%"
    },
    {
      "permission": "loans:approve",
      "name": "Approve Loans", 
      "tenantUsage": 14,
      "totalAssignments": 89,
      "averageRoleAssignments": 6.4,
      "category": "Loan Management",
      "adoptionRate": "93.3%"
    },
    {
      "permission": "system:advanced_config",
      "name": "Advanced System Configuration",
      "tenantUsage": 3,
      "totalAssignments": 5,
      "averageRoleAssignments": 1.7,
      "category": "System Administration", 
      "adoptionRate": "20%"
    }
  ],
  "trends": {
    "fastestGrowing": ["digital-payments:process", "mobile:approve"],
    "leastUsed": ["legacy:import", "deprecated:old_reports"],
    "highestRisk": ["system:delete_all", "finance:unlimited_transfer"]
  },
  "recommendations": [
    {
      "type": "deprecate",
      "permissions": ["legacy:import"],
      "reason": "Used by < 5% of tenants"
    },
    {
      "type": "promote",
      "permissions": ["audit-trail:advanced"], 
      "reason": "High compliance value, low adoption"
    }
  ],
  "summary": {
    "totalPermissions": 127,
    "activePermissions": 119,
    "averagePermissionsPerTenant": 47.3,
    "complianceScore": 94.2
  }
}
```

#### GET /platform/v1/roles/common-patterns (Common role patterns)
```json
{
  "commonRoles": [
    {
      "patternName": "Standard Loan Officer",
      "frequency": 14,
      "tenantAdoption": "93.3%",
      "commonPermissions": [
        "clients:view", "clients:create", "clients:edit",
        "loans:create", "loan-applications:process"
      ],
      "averagePermissionCount": 12,
      "variations": [
        {
          "name": "Senior Loan Officer",
          "additionalPermissions": ["loans:approve"],
          "frequency": 8
        }
      ]
    },
    {
      "patternName": "Credit Analyst",
      "frequency": 12,
      "tenantAdoption": "80%", 
      "commonPermissions": [
        "loans:review", "loans:approve", "credit-reports:view",
        "risk-assessment:perform"
      ],
      "averagePermissionCount": 8,
      "complianceNotes": "High compliance with BoZ segregation requirements"
    }
  ],
  "emergingPatterns": [
    {
      "patternName": "Digital Banking Officer",
      "frequency": 3,
      "isGrowing": true,
      "uniquePermissions": ["mobile:approve", "digital-payments:process"]
    }
  ],
  "recommendations": [
    {
      "type": "create_template",
      "patternName": "Standard Loan Officer",
      "reason": "High frequency pattern with consistent permissions"
    }
  ]
}
```

## Bridge Operation Validation

### **Permission Assignment Validation**
```csharp
public class PermissionAssignmentValidator
{
    public async Task<ValidationResult> ValidateAssignmentAsync(
        ApplicationRole role,
        string permission,
        string tenantId)
    {
        var result = new ValidationResult();
        
        // 1. Check permission exists and is available to tenant
        if (!await IsPermissionAvailableAsync(permission, tenantId))
        {
            result.AddError("Permission not available to current subscription tier");
        }
        
        // 2. Check for segregation of duties violations
        var conflicts = await CheckSegregationConflictsAsync(role, permission);
        result.AddWarnings(conflicts);
        
        // 3. Validate compliance requirements
        var complianceIssues = await CheckComplianceAsync(role, permission);
        result.AddWarnings(complianceIssues);
        
        // 4. Check tenant-specific rules
        var tenantRules = await ValidateTenantRulesAsync(role, permission, tenantId);
        result.Merge(tenantRules);
        
        return result;
    }
}
```

## Definition of Done
- [ ] **Single Permission Assignment**: Add/remove individual permissions from roles
- [ ] **Bulk Permission Operations**: Efficient bulk assignment with transaction support
- [ ] **Permission Discovery**: Find available permissions for role assignment
- [ ] **Role-Permission Matrix**: Visual representation of role-permission relationships
- [ ] **Assignment History**: Complete audit trail of permission assignments
- [ ] **Validation Engine**: Comprehensive validation of permission assignments
- [ ] **Usage Analytics**: Permission usage patterns and optimization insights
- [ ] **Cross-Tenant Patterns**: Identify common role patterns across tenants
- [ ] **Performance Optimization**: Fast permission queries using proper indexing
- [ ] **Error Handling**: Graceful handling of assignment conflicts and failures

## Dependencies
- IDENTITY-004: Platform Permission Catalog Management
- IDENTITY-005: Tenant Role Composition System
- IDENTITY-006: Rule-Based Authorization Engine
- ASP.NET Identity RoleManager and Claims system
- Validation engine for compliance and business rules
- Analytics and reporting infrastructure