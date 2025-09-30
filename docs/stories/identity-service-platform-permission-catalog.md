# Platform Permission Catalog Management - Multi-Tenant Enhancement

## Story Information
- **Story ID**: IDENTITY-004
- **Epic**: IntelliFin IdentityService API Development
- **Type**: Brownfield Multi-Tenant Enhancement
- **Priority**: High
- **Estimate**: 6-8 hours

## User Stories

### Primary Story - Platform Permission Management
As a **Platform Administrator** (IntelliFin Team),  
I want **comprehensive management of the master permission catalog**,  
So that **I can define atomic system capabilities that tenant admins can compose into custom roles**.

### Secondary Story - Tenant Permission Discovery
As a **Tenant Administrator** (Bank/MFI Admin),  
I want **access to browse and understand all available system permissions**,  
So that **I can make informed decisions when building roles for my organization**.

## Architecture Context - "Lego Brick" Permission System

**Core Concept**: **Decoupling Roles from Permissions**
- **Permissions**: Atomic, indivisible actions defined by platform (the "Lego bricks")
- **Roles**: Tenant-created collections of permissions that match their business structure

**Permission Design Principles:**
- Granular and atomic (single, indivisible right)
- Platform-defined and controlled
- Discoverable by tenant administrators
- Categorized by functional domain
- Immutable once deployed (versioning for changes)

## Acceptance Criteria

### **Platform Plane API Requirements** (`/platform/v1/*` endpoints)

**Permission Catalog Management:**
1. **POST /platform/v1/permissions** - Create new system permissions
2. **PUT /platform/v1/permissions/{permissionId}** - Update permission metadata
3. **DELETE /platform/v1/permissions/{permissionId}** - Deprecate permissions (soft delete)
4. **GET /platform/v1/permissions** - List all system permissions with filtering
5. **POST /platform/v1/permission-categories** - Organize permissions into functional categories

**Permission Deployment:**
6. **POST /platform/v1/permissions/deploy** - Deploy permission changes across all tenants
7. **GET /platform/v1/permissions/impact-analysis** - Analyze permission change impacts
8. **POST /platform/v1/permissions/rollback** - Rollback permission deployments

**Platform Access Requirements:**
9. All endpoints require JWT with `PlatformAdmin` role claim
10. Permission changes require dual approval for production deployments
11. Comprehensive audit logging for all permission catalog changes

### **Tenant Plane API Requirements** (`/v1/*` endpoints)

**Permission Discovery:**
12. **GET /v1/permissions/catalog** - Browse available permissions (read-only)
13. **GET /v1/permissions/categories** - Get permission categories for UI organization
14. **GET /v1/permissions/search** - Search permissions by name, category, or functionality
15. **GET /v1/permissions/{permissionId}/details** - Get detailed permission information

**Tenant Context Requirements:**
16. All endpoints require JWT with `tenant_id` claim
17. Permission catalog filtered by tenant's subscription tier
18. Only permissions available to tenant's feature set shown
19. Permission usage analytics tracked per tenant

## Technical Architecture

### **Permission Definition System**
```csharp
// Static permission constants (compile-time safety)
public static class SystemPermissions
{
    // Client Management
    public const string ClientsView = "clients:view";
    public const string ClientsCreate = "clients:create"; 
    public const string ClientsEdit = "clients:edit";
    public const string ClientsViewPii = "clients:view_pii";
    public const string ClientsEditContactInfo = "clients:edit_contact_info";
    
    // Loan Management
    public const string LoansCreate = "loans:create";
    public const string LoansApprove = "loans:approve";
    public const string LoansReject = "loans:reject";
    public const string LoansDisbursе = "loans:disburse";
    
    // Reporting
    public const string ReportsGeneratePortfolio = "reports:generate_portfolio";
    public const string ReportsViewSensitive = "reports:view_sensitive";
    public const string ReportsExport = "reports:export";
    
    // System Administration  
    public const string SystemConfigView = "system:config_view";
    public const string SystemConfigEdit = "system:config_edit";
    public const string SystemUsersManage = "system:users_manage";
}
```

### **Permission Metadata Model**
```csharp
public class SystemPermission
{
    public string Id { get; set; }  // e.g., "clients:view"
    public string Name { get; set; } // e.g., "View Client Information"
    public string Description { get; set; }
    public string Category { get; set; } // e.g., "Client Management"
    public string[] RequiredFeatures { get; set; } // Feature flags
    public SubscriptionTier MinimumTier { get; set; }
    public bool IsDeprecated { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public PermissionRiskLevel RiskLevel { get; set; }
}

public enum PermissionRiskLevel
{
    Low,        // View operations
    Medium,     // Create/Edit operations  
    High,       // Delete/Financial operations
    Critical    // System configuration
}
```

## API Design Preview

### **Platform Plane APIs** (`/platform/v1/*`)

#### POST /platform/v1/permissions (Create system permission)
```json
{
  "id": "loans:approve_high_value",
  "name": "Approve High-Value Loans", 
  "description": "Approve loans above institutional threshold",
  "category": "Loan Management",
  "requiredFeatures": ["loan-origination", "credit-assessment"],
  "minimumTier": "professional",
  "riskLevel": "high",
  "businessRules": {
    "supportsValueClaims": true,
    "defaultRules": {
      "max_amount": null,  // No default limit
      "requires_dual_approval": true
    }
  }
}
```

#### GET /platform/v1/permissions (Master permission catalog)
```json
{
  "permissions": [
    {
      "id": "clients:view",
      "name": "View Client Information",
      "description": "Access to view client basic information", 
      "category": "Client Management",
      "riskLevel": "low",
      "usage": {
        "activeTenants": 14,
        "totalRoleAssignments": 156
      }
    },
    {
      "id": "loans:approve",
      "name": "Approve Loans",
      "description": "Authority to approve loan applications",
      "category": "Loan Management", 
      "riskLevel": "high",
      "businessRules": {
        "supportsValueClaims": true,
        "commonRules": ["approval_limit", "risk_grade_limit"]
      }
    }
  ],
  "categories": [
    {
      "name": "Client Management",
      "permissionCount": 12,
      "description": "Customer data and relationship management"
    },
    {
      "name": "Loan Management", 
      "permissionCount": 18,
      "description": "Loan origination, approval, and servicing"
    }
  ],
  "metadata": {
    "totalPermissions": 127,
    "activePermissions": 119,
    "deprecatedPermissions": 8
  }
}
```

### **Tenant Plane APIs** (`/v1/*`)

#### GET /v1/permissions/catalog (Tenant-filtered permission catalog)
```json
{
  "permissions": [
    {
      "id": "clients:view",
      "name": "View Client Information",
      "description": "Access to view client basic information",
      "category": "Client Management",
      "riskLevel": "low",
      "availableToTenant": true,
      "currentlyUsed": true,
      "assignedRoles": ["Loan Officer", "Branch Manager"]
    },
    {
      "id": "loans:approve_enterprise", 
      "name": "Enterprise Loan Approval",
      "description": "Approve loans with enterprise-tier limits",
      "category": "Loan Management",
      "riskLevel": "high", 
      "availableToTenant": false,
      "reason": "Requires Enterprise subscription tier",
      "upgradeRequired": true
    }
  ],
  "tenantContext": {
    "tenantId": "abc-123-456",
    "subscriptionTier": "professional",
    "availablePermissions": 89,
    "enterpriseOnlyPermissions": 23
  },
  "categories": [
    {
      "name": "Client Management",
      "availablePermissions": 10,
      "totalPermissions": 12,
      "restrictedPermissions": ["clients:delete", "clients:merge"]
    }
  ]
}
```

#### GET /v1/permissions/search (Search available permissions)
```json
{
  "query": "approve loan",
  "results": [
    {
      "id": "loans:approve",
      "name": "Approve Loans",
      "description": "Standard loan approval authority",
      "category": "Loan Management",
      "relevanceScore": 0.95,
      "matchType": "name_description"
    },
    {
      "id": "loans:approve_emergency",
      "name": "Emergency Loan Approval", 
      "description": "Approve loans during system emergencies",
      "category": "Loan Management",
      "relevanceScore": 0.87,
      "matchType": "description"
    }
  ],
  "suggestions": [
    "loan disbursement",
    "credit approval", 
    "loan rejection"
  ]
}
```

## Integration Requirements

### **Static Permission Validation**
```csharp
// Compile-time permission validation
[Authorize(Policy = SystemPermissions.LoansApprove)]
public async Task<IActionResult> ApproveLoan(int loanId)
{
    // Implementation
}

// Runtime permission validation  
public class PermissionValidator
{
    public static bool IsValidPermission(string permission)
    {
        return typeof(SystemPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Any(f => f.GetValue(null)?.ToString() == permission);
    }
}
```

### **Permission Deployment Pipeline**
```csharp
public class PermissionDeploymentService
{
    public async Task<DeploymentResult> DeployPermissionChanges(
        PermissionChangeSet changes, 
        bool requiresApproval = true)
    {
        // 1. Validate permission format and conflicts
        // 2. Analyze impact across all tenants
        // 3. Create deployment plan
        // 4. Execute with rollback capability
        // 5. Update tenant permission caches
    }
}
```

## Definition of Done
- [ ] **Platform Permission CRUD**: Full lifecycle management of system permissions
- [ ] **Static Permission Constants**: Compile-time safe permission definitions
- [ ] **Tenant Permission Discovery**: Read-only catalog browsing for tenant admins
- [ ] **Permission Categorization**: Logical organization of permissions by domain
- [ ] **Subscription Tier Filtering**: Permissions filtered by tenant capabilities
- [ ] **Permission Search**: Advanced search and filtering capabilities
- [ ] **Deployment Pipeline**: Safe permission deployment with rollback
- [ ] **Impact Analysis**: Understanding permission change effects across tenants
- [ ] **Audit Trail**: Complete logging of permission catalog changes

## Dependencies
- Existing IdentityService infrastructure
- Tenant subscription management system
- Feature flag system for permission availability
- Audit logging framework
- Cache invalidation system for permission updates