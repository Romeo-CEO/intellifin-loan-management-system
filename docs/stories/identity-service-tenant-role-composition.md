# Tenant Role Composition System - Multi-Tenant Enhancement

## Story Information
- **Story ID**: IDENTITY-005
- **Epic**: IntelliFin IdentityService API Development
- **Type**: Brownfield Multi-Tenant Enhancement
- **Priority**: High
- **Estimate**: 8-10 hours

## User Stories

### Primary Story - Tenant Role Builder
As a **Tenant Administrator** (Bank/MFI Admin),  
I want **to create custom roles by combining platform permissions**,  
So that **I can match our unique organizational hierarchy and job functions**.

### Secondary Story - Role Template Management  
As a **Platform Administrator** (IntelliFin Team),  
I want **to provide role templates and best practice guidance**,  
So that **tenants can quickly set up compliant organizational structures**.

## Architecture Context - Custom Role Composition

**Core Concept**: **Tenant-Defined Roles with Platform Permissions**
- **Roles**: Custom collections of permissions created by tenant admins
- **Composition**: Building roles from atomic platform permissions
- **Flexibility**: Each tenant creates roles that match their business structure
- **Compliance**: Built-in BoZ compliance validation and recommendations

**Role Composition Principles:**
- Tenant admins have full control over role names and structures
- Roles are built from platform-defined atomic permissions
- Segregation of duties enforced during role creation
- Role templates provided for common financial institution structures
- Compliance validation integrated into role builder

## Acceptance Criteria

### **Tenant Plane API Requirements** (`/v1/*` endpoints)

**Custom Role Management:**
1. **POST /v1/roles** - Create custom role with name and description
2. **PUT /v1/roles/{roleId}** - Update role metadata (name, description)
3. **DELETE /v1/roles/{roleId}** - Delete role (with user assignment validation)
4. **GET /v1/roles** - List tenant's custom roles with permission summaries
5. **GET /v1/roles/{roleId}** - Get detailed role information with all permissions

**Role Composition:**
6. **POST /v1/roles/{roleId}/permissions** - Add permissions to role
7. **DELETE /v1/roles/{roleId}/permissions/{permissionId}** - Remove permission from role
8. **PUT /v1/roles/{roleId}/permissions** - Bulk update role permissions
9. **GET /v1/roles/{roleId}/permissions** - List all permissions assigned to role

**Role Validation:**
10. **POST /v1/roles/{roleId}/validate** - Validate role for compliance and conflicts
11. **GET /v1/roles/compliance-check** - Check all roles against BoZ requirements
12. **POST /v1/roles/{roleId}/preview** - Preview role capabilities before assignment

**Tenant Context Requirements:**
13. All endpoints require JWT with `tenant_id` claim
14. Roles automatically scoped to tenant
15. Permission assignments validated against tenant's available permissions
16. Role names must be unique within tenant

### **Platform Plane API Requirements** (`/platform/v1/*` endpoints)

**Role Template Management:**
17. **POST /platform/v1/role-templates** - Create role templates for tenant guidance
18. **GET /platform/v1/role-templates** - List available role templates
19. **PUT /platform/v1/role-templates/{templateId}** - Update role template
20. **GET /platform/v1/tenants/{tenantId}/role-analysis** - Analyze tenant's role structure

**Platform Access Requirements:**
21. Template management requires `PlatformAdmin` role
22. Tenant role analysis requires explicit tenant specification
23. Comprehensive audit logging for template changes

## Technical Architecture

### **Tenant Role Data Model**
```csharp
public class ApplicationRole : IdentityRole
{
    public Guid? TenantId { get; set; }  // NULL for platform roles
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public bool IsCustom { get; set; } = true;  // false for system/template roles
    public RoleCategory Category { get; set; }
    public int UserCount { get; set; } // Denormalized for performance
    public bool IsActive { get; set; } = true;
}

public enum RoleCategory
{
    LoanOfficers,
    CreditManagement, 
    Finance,
    Operations,
    Compliance,
    Management,
    Administration
}
```

### **Role-Permission Bridge (IdentityRoleClaim)**
```csharp
// Built-in ASP.NET Identity table used for permission assignments
public class IdentityRoleClaim<TKey>
{
    public int Id { get; set; }
    public TKey RoleId { get; set; }  // Links to ApplicationRole
    public string ClaimType { get; set; } = "permission";
    public string ClaimValue { get; set; } // The permission string
}

// Extension for role composition
public class RolePermissionAssignment
{
    public string RoleId { get; set; }
    public string Permission { get; set; }
    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### **Role Template System**
```csharp
public class RoleTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public RoleCategory Category { get; set; }
    public string[] RecommendedPermissions { get; set; }
    public string[] RequiredPermissions { get; set; } // BoZ compliance
    public string[] ProhibitedPermissions { get; set; } // Segregation of duties
    public SubscriptionTier MinimumTier { get; set; }
    public ComplianceFramework[] ApplicableFrameworks { get; set; }
}
```

## API Design Preview

### **Tenant Plane APIs** (`/v1/*`)

#### POST /v1/roles (Create custom role)
```json
{
  "name": "Senior Credit Analyst",
  "description": "Experienced credit analyst with higher approval limits",
  "category": "CreditManagement"
}

// Response
{
  "roleId": "role-123-456",
  "name": "Senior Credit Analyst", 
  "description": "Experienced credit analyst with higher approval limits",
  "tenantId": "abc-123-456",
  "category": "CreditManagement",
  "createdAt": "2024-01-16T10:00:00Z",
  "createdBy": "tenant-admin-789",
  "userCount": 0,
  "permissionCount": 0,
  "isActive": true
}
```

#### POST /v1/roles/{roleId}/permissions (Add permissions to role)
```json
{
  "permissions": [
    "clients:view",
    "clients:edit", 
    "loans:create",
    "loans:approve",
    "credit-reports:view"
  ]
}

// Response
{
  "roleId": "role-123-456",
  "addedPermissions": [
    {
      "permission": "clients:view",
      "added": true,
      "reason": "Successfully added"
    },
    {
      "permission": "loans:approve", 
      "added": true,
      "reason": "Successfully added"
    },
    {
      "permission": "system:config_edit",
      "added": false,
      "reason": "Permission not available to tenant subscription tier"
    }
  ],
  "validationResults": {
    "complianceIssues": [],
    "segregationWarnings": [],
    "recommendedAdditions": ["credit-reports:export"]
  }
}
```

#### GET /v1/roles (List tenant's custom roles)
```json
{
  "roles": [
    {
      "roleId": "role-123-456",
      "name": "Senior Credit Analyst",
      "description": "Experienced credit analyst with higher approval limits", 
      "category": "CreditManagement",
      "userCount": 3,
      "permissionCount": 12,
      "lastModified": "2024-01-15T14:30:00Z",
      "complianceStatus": "compliant",
      "isActive": true
    },
    {
      "roleId": "role-789-012", 
      "name": "Branch Manager",
      "description": "Manages branch operations and staff",
      "category": "Management", 
      "userCount": 2,
      "permissionCount": 25,
      "lastModified": "2024-01-14T09:15:00Z",
      "complianceStatus": "warning",
      "complianceIssues": ["Missing audit trail permission"],
      "isActive": true
    }
  ],
  "summary": {
    "totalRoles": 8,
    "activeRoles": 7,
    "totalUsers": 45,
    "complianceIssues": 1
  },
  "tenantContext": {
    "tenantId": "abc-123-456",
    "canCreateRoles": true,
    "maxRoles": 20,
    "rolesUsed": 8
  }
}
```

#### POST /v1/roles/{roleId}/validate (Validate role compliance)
```json
// Response
{
  "roleId": "role-123-456", 
  "roleName": "Senior Credit Analyst",
  "validationResults": {
    "isCompliant": true,
    "overallScore": 85,
    "checks": [
      {
        "checkType": "segregation_of_duties",
        "passed": true,
        "message": "No conflicting permissions detected"
      },
      {
        "checkType": "boz_requirements",
        "passed": true, 
        "message": "All BoZ mandatory permissions present"
      },
      {
        "checkType": "subscription_tier",
        "passed": true,
        "message": "All permissions available to current tier"
      }
    ],
    "recommendations": [
      {
        "type": "add_permission",
        "permission": "audit-trail:view",
        "reason": "Recommended for compliance officers"
      }
    ],
    "warnings": [
      {
        "type": "high_privilege",
        "message": "Role has high-risk permissions, ensure appropriate users"
      }
    ]
  }
}
```

### **Platform Plane APIs** (`/platform/v1/*`)

#### GET /platform/v1/role-templates (Available role templates)
```json
{
  "templates": [
    {
      "templateId": "loan-officer-standard",
      "name": "Loan Officer (Standard)",
      "description": "Standard loan officer role for most MFIs",
      "category": "LoanOfficers",
      "minimumTier": "starter",
      "recommendedPermissions": [
        "clients:view", "clients:create", "clients:edit",
        "loans:create", "loan-applications:process"
      ],
      "requiredPermissions": [
        "audit-trail:create"
      ],
      "prohibitedPermissions": [
        "loans:approve", "loans:disburse"
      ],
      "usageStats": {
        "tenantsUsing": 12,
        "averageUserCount": 8
      }
    },
    {
      "templateId": "head-of-credit", 
      "name": "Head of Credit",
      "description": "Senior credit management role with approval authority",
      "category": "CreditManagement",
      "minimumTier": "professional",
      "recommendedPermissions": [
        "loans:approve", "credit-policies:manage", 
        "portfolio:analyze", "risk:assess"
      ],
      "complianceNotes": "Must maintain segregation from disbursement functions"
    }
  ],
  "categories": [
    {
      "category": "LoanOfficers",
      "templateCount": 4,
      "description": "Front-line loan processing staff"
    },
    {
      "category": "CreditManagement", 
      "templateCount": 6,
      "description": "Credit analysis and approval roles"
    }
  ]
}
```

#### GET /platform/v1/tenants/{tenantId}/role-analysis (Tenant role analysis)
```json
{
  "tenantId": "abc-123-456",
  "tenantName": "First National Bank Zambia",
  "roleAnalysis": {
    "totalRoles": 8,
    "customRoles": 6,
    "templateBasedRoles": 2,
    "complianceScore": 78,
    "lastReview": "2024-01-10T00:00:00Z"
  },
  "complianceIssues": [
    {
      "severity": "medium",
      "roleId": "role-789-012",
      "roleName": "Branch Manager", 
      "issue": "Missing mandatory audit trail permission",
      "recommendation": "Add audit-trail:view permission"
    }
  ],
  "segregationIssues": [],
  "recommendations": [
    {
      "type": "role_template",
      "templateId": "compliance-officer-boz",
      "reason": "No dedicated compliance role detected"
    }
  ],
  "trends": {
    "rolesCreatedLastMonth": 2,
    "permissionChangesLastMonth": 5,
    "mostUsedPermissions": ["clients:view", "loans:create", "reports:view"]
  }
}
```

## Integration with Permission System

### **Role Permission Assignment Service**
```csharp
public class RoleCompositionService
{
    public async Task<RolePermissionResult> AddPermissionsToRoleAsync(
        string roleId, 
        string[] permissions,
        string tenantId)
    {
        // 1. Validate role belongs to tenant
        // 2. Check permissions are available to tenant
        // 3. Validate compliance and segregation rules
        // 4. Add permission claims to role
        // 5. Update role metrics
        // 6. Log changes for audit
    }
    
    public async Task<ComplianceValidationResult> ValidateRoleComplianceAsync(
        string roleId)
    {
        // 1. Check BoZ mandatory permissions
        // 2. Validate segregation of duties
        // 3. Check subscription tier restrictions
        // 4. Generate compliance score
        // 5. Provide recommendations
    }
}
```

## Definition of Done
- [ ] **Tenant Role CRUD**: Complete role lifecycle management for tenants
- [ ] **Permission Assignment**: Add/remove permissions from roles via Claims
- [ ] **Role Templates**: Platform-provided templates for quick setup
- [ ] **Compliance Validation**: BoZ compliance checking during role creation
- [ ] **Segregation Enforcement**: Automatic detection of duty conflicts
- [ ] **Role Builder UI Support**: APIs optimized for drag-drop role building
- [ ] **Bulk Operations**: Efficient bulk permission assignment
- [ ] **Role Analysis**: Platform insights into tenant role structures
- [ ] **Audit Trail**: Complete logging of role composition changes

## Dependencies
- IDENTITY-004: Platform Permission Catalog Management
- Existing ASP.NET Identity Role and Claim infrastructure
- BoZ compliance rule engine
- Tenant subscription management system
- Audit logging framework