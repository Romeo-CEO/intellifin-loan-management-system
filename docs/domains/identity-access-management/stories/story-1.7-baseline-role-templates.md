# Story 1.7: Baseline Role Templates and Seed Data

## Story Information

**Epic:** Foundation Setup (Epic 1)  
**Story ID:** 1.7  
**Story Name:** Baseline Role Templates and Seed Data  
**Priority:** High  
**Estimated Effort:** 3 story points (5-8 hours)  
**Dependencies:** Story 1.1 (Database Schema Extensions)  
**Blocks:** Stories 1.10 (SoD Enforcement), 1.12 (Migration), downstream authorization

---

## Story Description

As a **System Administrator**, I want **baseline role templates and default SoD rules seeded during deployment** so that **consistent role definitions are available across all environments without manual configuration**.

### Business Value

- Standardizes role definitions across development, staging, and production environments
- Reduces deployment time by eliminating manual role configuration
- Provides production-ready permission mappings for common user personas
- Establishes foundation for Separation of Duties enforcement
- Ensures compliance with baseline security policies

### User Story

```
Given a freshly deployed IntelliFin instance
When the Identity Service starts for the first time
Then baseline roles and SoD rules should be automatically seeded
And the seed operation should be idempotent
And existing roles should not be modified
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** 6 baseline roles created in AspNetRoles:
  - System Administrator
  - Loan Officer
  - Underwriter
  - Finance Manager
  - Collections Officer
  - Compliance Officer

- [ ] **AC2:** Permission mappings created in AspNetRoleClaims for each role with appropriate atomic permissions from the existing 80+ permission catalog

- [ ] **AC3:** 4 default SoD rules seeded into SoDRules table:
  - `sod-loan-create-approve`: Prevents loans:create + loans:approve
  - `sod-disbursement-approval`: Prevents loans:disburse + loans:approve
  - `sod-gl-posting`: Prevents gl:post + gl:reverse
  - `sod-payment-reconciliation`: Prevents payments:create + payments:reconcile

- [ ] **AC4:** Seed data is idempotent - running multiple times doesn't create duplicates

- [ ] **AC5:** Audit events logged for all seed data creation

### Non-Functional Criteria

- [ ] **AC6:** Seed operation completes in <10 seconds

- [ ] **AC7:** Existing roles and permissions are not modified or deleted

- [ ] **AC8:** Existing user role assignments remain unchanged

- [ ] **AC9:** Seed data can be configured via JSON file for environment-specific customization

- [ ] **AC10:** Transaction-based seeding - all or nothing (no partial seeds)

---

## Technical Specification

### Seed Data Structure

#### Role Definitions

```csharp
public class BaselineRoleDefinition
{
    public string RoleName { get; set; }
    public string Description { get; set; }
    public List<string> Permissions { get; set; }
}
```

**Baseline Roles Configuration:**

1. **System Administrator**
   - All permissions (platform:*, users:*, roles:*, tenants:*, services:*, audit:*)
   - Purpose: Full system access for platform administration

2. **Loan Officer**
   - loans:view, loans:create, loans:update, clients:view, clients:create, clients:update, documents:view, documents:upload
   - Purpose: Front-office loan origination and client management

3. **Underwriter**
   - loans:view, loans:update, loans:approve, loans:reject, documents:view, credit:view, credit:request
   - Purpose: Loan evaluation and approval decisions

4. **Finance Manager**
   - loans:view, gl:view, gl:post, payments:view, payments:create, disbursements:view, disbursements:approve, reports:financial
   - Purpose: Financial operations and general ledger management

5. **Collections Officer**
   - loans:view, payments:view, payments:create, collections:view, collections:create, collections:update, communications:send
   - Purpose: Late payment recovery and collection management

6. **Compliance Officer**
   - loans:view, clients:view, audit:view, audit:export, reports:compliance, reports:regulatory, sod:view, sod:override
   - Purpose: Regulatory compliance monitoring and audit review

#### SoD Rule Definitions

```csharp
public class SoDRuleDefinition
{
    public string RuleName { get; set; }
    public string Description { get; set; }
    public string[] ConflictingPermissions { get; set; }
    public string Enforcement { get; set; } // "strict" or "warning"
}
```

**Default SoD Rules:**

1. **sod-loan-create-approve**
   - Conflicting: ["loans:create", "loans:approve"]
   - Enforcement: strict
   - Rationale: Prevents fraud through self-approval

2. **sod-disbursement-approval**
   - Conflicting: ["loans:disburse", "loans:approve"]
   - Enforcement: strict
   - Rationale: Separates approval from fund release

3. **sod-gl-posting**
   - Conflicting: ["gl:post", "gl:reverse"]
   - Enforcement: strict
   - Rationale: Prevents unauthorized GL manipulation

4. **sod-payment-reconciliation**
   - Conflicting: ["payments:create", "payments:reconcile"]
   - Enforcement: warning
   - Rationale: Best practice separation (not strictly enforced)

---

## Implementation Steps

### Step 1: Create Seed Data Configuration File

**Location:** `IntelliFin.IdentityService/Data/Seeds/BaselineRolesSeed.json`

```json
{
  "roles": [
    {
      "roleName": "System Administrator",
      "description": "Full system access for platform administration",
      "permissions": [
        "platform:*", "users:*", "roles:*", "tenants:*", 
        "services:*", "audit:*", "system:*"
      ]
    },
    {
      "roleName": "Loan Officer",
      "description": "Front-office loan origination and client management",
      "permissions": [
        "loans:view", "loans:create", "loans:update",
        "clients:view", "clients:create", "clients:update",
        "documents:view", "documents:upload"
      ]
    },
    {
      "roleName": "Underwriter",
      "description": "Loan evaluation and approval decisions",
      "permissions": [
        "loans:view", "loans:update", "loans:approve", "loans:reject",
        "documents:view", "credit:view", "credit:request"
      ]
    },
    {
      "roleName": "Finance Manager",
      "description": "Financial operations and general ledger management",
      "permissions": [
        "loans:view", "gl:view", "gl:post",
        "payments:view", "payments:create",
        "disbursements:view", "disbursements:approve",
        "reports:financial"
      ]
    },
    {
      "roleName": "Collections Officer",
      "description": "Late payment recovery and collection management",
      "permissions": [
        "loans:view", "payments:view", "payments:create",
        "collections:view", "collections:create", "collections:update",
        "communications:send"
      ]
    },
    {
      "roleName": "Compliance Officer",
      "description": "Regulatory compliance monitoring and audit review",
      "permissions": [
        "loans:view", "clients:view",
        "audit:view", "audit:export",
        "reports:compliance", "reports:regulatory",
        "sod:view", "sod:override"
      ]
    }
  ],
  "sodRules": [
    {
      "ruleName": "sod-loan-create-approve",
      "description": "Prevent same user from creating and approving loans",
      "conflictingPermissions": ["loans:create", "loans:approve"],
      "enforcement": "strict"
    },
    {
      "ruleName": "sod-disbursement-approval",
      "description": "Prevent same user from approving and disbursing loans",
      "conflictingPermissions": ["loans:disburse", "loans:approve"],
      "enforcement": "strict"
    },
    {
      "ruleName": "sod-gl-posting",
      "description": "Prevent same user from posting and reversing GL entries",
      "conflictingPermissions": ["gl:post", "gl:reverse"],
      "enforcement": "strict"
    },
    {
      "ruleName": "sod-payment-reconciliation",
      "description": "Warn when same user can create and reconcile payments",
      "conflictingPermissions": ["payments:create", "payments:reconcile"],
      "enforcement": "warning"
    }
  ]
}
```

### Step 2: Create Seed Service

**Location:** `IntelliFin.IdentityService/Services/IBaselineSeedService.cs`

```csharp
using IntelliFin.IdentityService.Models.Domain;

namespace IntelliFin.IdentityService.Services;

public interface IBaselineSeedService
{
    /// <summary>
    /// Seeds baseline roles and SoD rules from configuration
    /// </summary>
    Task<SeedResult> SeedBaselineDataAsync();
    
    /// <summary>
    /// Validates seed data without applying changes
    /// </summary>
    Task<SeedValidationResult> ValidateSeedDataAsync();
}

public class SeedResult
{
    public int RolesCreated { get; set; }
    public int PermissionsCreated { get; set; }
    public int SoDRulesCreated { get; set; }
    public int RolesSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}
```

**Implementation:** `IntelliFin.IdentityService/Services/BaselineSeedService.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IntelliFin.IdentityService.Data;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Models.Domain;
using System.Security.Claims;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

public class BaselineSeedService : IBaselineSeedService
{
    private readonly LmsDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<BaselineSeedService> _logger;

    public BaselineSeedService(
        LmsDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IAuditService auditService,
        ILogger<BaselineSeedService> logger)
    {
        _context = context;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<SeedResult> SeedBaselineDataAsync()
    {
        var result = new SeedResult();
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Load seed configuration
            var seedConfig = LoadSeedConfiguration();
            
            // Seed roles and permissions
            foreach (var roleDefinition in seedConfig.Roles)
            {
                var (created, permissions) = await SeedRoleAsync(roleDefinition);
                if (created)
                {
                    result.RolesCreated++;
                    result.PermissionsCreated += permissions;
                }
                else
                {
                    result.RolesSkipped++;
                }
            }
            
            // Seed SoD rules
            foreach (var sodRule in seedConfig.SodRules)
            {
                var created = await SeedSoDRuleAsync(sodRule);
                if (created) result.SoDRulesCreated++;
            }
            
            await transaction.CommitAsync();
            
            _logger.LogInformation(
                "Baseline seed completed: {RolesCreated} roles, {PermissionsCreated} permissions, {SoDRulesCreated} SoD rules",
                result.RolesCreated, result.PermissionsCreated, result.SoDRulesCreated);
            
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.Errors.Add($"Seed failed: {ex.Message}");
            _logger.LogError(ex, "Baseline seed operation failed");
            return result;
        }
    }

    private async Task<(bool Created, int PermissionCount)> SeedRoleAsync(RoleDefinition roleDefinition)
    {
        // Check if role already exists
        if (await _roleManager.RoleExistsAsync(roleDefinition.RoleName))
        {
            _logger.LogInformation("Role {RoleName} already exists, skipping", roleDefinition.RoleName);
            return (false, 0);
        }
        
        // Create role
        var role = new ApplicationRole
        {
            Name = roleDefinition.RoleName,
            Description = roleDefinition.Description,
            IsSystemRole = true
        };
        
        var createResult = await _roleManager.CreateAsync(role);
        if (!createResult.Succeeded)
        {
            throw new Exception($"Failed to create role {roleDefinition.RoleName}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }
        
        // Add permission claims
        int permissionCount = 0;
        foreach (var permission in roleDefinition.Permissions)
        {
            await _roleManager.AddClaimAsync(role, new Claim("permission", permission));
            permissionCount++;
        }
        
        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = "SYSTEM",
            Action = "RoleCreated",
            Entity = "Role",
            EntityId = role.Id,
            Details = JsonSerializer.Serialize(new { roleDefinition.RoleName, PermissionCount = permissionCount })
        });
        
        return (true, permissionCount);
    }

    private async Task<bool> SeedSoDRuleAsync(SoDRuleDefinition sodDefinition)
    {
        // Check if rule already exists
        var exists = await _context.SoDRules.AnyAsync(r => r.RuleName == sodDefinition.RuleName);
        if (exists)
        {
            _logger.LogInformation("SoD rule {RuleName} already exists, skipping", sodDefinition.RuleName);
            return false;
        }
        
        var sodRule = new SoDRule
        {
            RuleId = Guid.NewGuid(),
            RuleName = sodDefinition.RuleName,
            Description = sodDefinition.Description,
            ConflictingPermissions = JsonSerializer.Serialize(sodDefinition.ConflictingPermissions),
            Enforcement = sodDefinition.Enforcement,
            IsActive = true
        };
        
        _context.SoDRules.Add(sodRule);
        await _context.SaveChangesAsync();
        
        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = "SYSTEM",
            Action = "SoDRuleCreated",
            Entity = "SoDRule",
            EntityId = sodRule.RuleId.ToString(),
            Details = JsonSerializer.Serialize(sodDefinition)
        });
        
        return true;
    }

    private BaselineSeedConfiguration LoadSeedConfiguration()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seeds", "BaselineRolesSeed.json");
        var configJson = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<BaselineSeedConfiguration>(configJson)
            ?? throw new Exception("Failed to deserialize seed configuration");
    }

    public async Task<SeedValidationResult> ValidateSeedDataAsync()
    {
        // Validation logic (dry-run check)
        var result = new SeedValidationResult();
        var config = LoadSeedConfiguration();
        
        // Validate role names don't conflict
        foreach (var role in config.Roles)
        {
            var exists = await _roleManager.RoleExistsAsync(role.RoleName);
            result.AddRoleCheck(role.RoleName, !exists);
        }
        
        // Validate SoD rules don't conflict
        foreach (var sodRule in config.SodRules)
        {
            var exists = await _context.SoDRules.AnyAsync(r => r.RuleName == sodRule.RuleName);
            result.AddSoDCheck(sodRule.RuleName, !exists);
        }
        
        return result;
    }
}
```

### Step 3: Register Service and Configure Startup Hook

**Location:** `IntelliFin.IdentityService/ServiceCollectionExtensions.cs`

```csharp
// Register seed service
services.AddScoped<IBaselineSeedService, BaselineSeedService>();
```

**Location:** `IntelliFin.IdentityService/Program.cs`

```csharp
// After app.Build()
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SeedBaselineData"))
{
    using var scope = app.Services.CreateScope();
    var seedService = scope.ServiceProvider.GetRequiredService<IBaselineSeedService>();
    var seedResult = await seedService.SeedBaselineDataAsync();
    
    if (seedResult.Success)
    {
        app.Logger.LogInformation("Baseline seed completed: {Result}", seedResult);
    }
    else
    {
        app.Logger.LogWarning("Baseline seed had errors: {Errors}", string.Join(", ", seedResult.Errors));
    }
}
```

### Step 4: Create Management Endpoint

**Location:** `IntelliFin.IdentityService/Controllers/Platform/SeedController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

[ApiController]
[Route("api/platform/seed")]
[Authorize(Policy = "RequireSystemAdmin")]
public class SeedController : ControllerBase
{
    private readonly IBaselineSeedService _seedService;

    public SeedController(IBaselineSeedService seedService)
    {
        _seedService = seedService;
    }

    [HttpPost("baseline")]
    public async Task<IActionResult> SeedBaselineData()
    {
        var result = await _seedService.SeedBaselineDataAsync();
        
        if (result.Success)
        {
            return Ok(result);
        }
        
        return BadRequest(result);
    }

    [HttpPost("baseline/validate")]
    public async Task<IActionResult> ValidateBaselineData()
    {
        var result = await _seedService.ValidateSeedDataAsync();
        return Ok(result);
    }
}
```

---

## Testing Requirements

### Unit Tests

**Location:** `IntelliFin.IdentityService.Tests/Services/BaselineSeedServiceTests.cs`

```csharp
[Fact]
public async Task SeedBaselineData_CreatesAllRoles()
{
    // Arrange
    var service = CreateSeedService();
    
    // Act
    var result = await service.SeedBaselineDataAsync();
    
    // Assert
    result.Success.Should().BeTrue();
    result.RolesCreated.Should().Be(6);
    result.SoDRulesCreated.Should().Be(4);
}

[Fact]
public async Task SeedBaselineData_IsIdempotent()
{
    // Arrange
    var service = CreateSeedService();
    
    // Act
    var result1 = await service.SeedBaselineDataAsync();
    var result2 = await service.SeedBaselineDataAsync();
    
    // Assert
    result2.RolesCreated.Should().Be(0);
    result2.RolesSkipped.Should().Be(6);
}

[Fact]
public async Task SeedBaselineData_RollsBackOnError()
{
    // Arrange
    var service = CreateSeedServiceWithFailure();
    
    // Act
    var result = await service.SeedBaselineDataAsync();
    
    // Assert
    result.Success.Should().BeFalse();
    result.Errors.Should().NotBeEmpty();
    
    // Verify no partial data
    var context = GetContext();
    var roleCount = await context.Roles.CountAsync();
    var initialCount = 0; // Assuming no roles before seed
    roleCount.Should().Be(initialCount);
}
```

### Integration Tests

```powershell
# Test seed endpoint
curl -X POST https://localhost:5001/api/platform/seed/baseline `
  -H "Authorization: Bearer $ADMIN_TOKEN"

# Verify roles created
curl https://localhost:5001/api/roles `
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

---

## Integration Verification

### Checkpoint 1: All Roles Created

**Verification:**

```sql
SELECT Name, Description FROM AspNetRoles 
WHERE Name IN (
    'System Administrator', 
    'Loan Officer', 
    'Underwriter', 
    'Finance Manager', 
    'Collections Officer', 
    'Compliance Officer'
);
```

**Success Criteria:** 6 roles returned

### Checkpoint 2: Permission Claims Exist

**Verification:**

```sql
SELECT r.Name, COUNT(c.Id) AS PermissionCount
FROM AspNetRoles r
INNER JOIN AspNetRoleClaims c ON r.Id = c.RoleId
WHERE c.ClaimType = 'permission'
GROUP BY r.Name;
```

**Success Criteria:** Each role has >0 permissions

### Checkpoint 3: SoD Rules Seeded

**Verification:**

```sql
SELECT RuleName, Enforcement, IsActive FROM SoDRules;
```

**Success Criteria:** 4 rules returned, all IsActive = 1

### Checkpoint 4: Existing Data Unchanged

**Verification:**

```sql
-- Check for modified timestamps on existing roles
SELECT Name, ModifiedAt FROM AspNetRoles 
WHERE ModifiedAt > DATEADD(minute, -5, GETUTCDATE());
```

**Success Criteria:** Only new roles appear (not pre-existing ones)

### Checkpoint 5: Idempotency Test

**Action:** Run seed operation 3 times consecutively

**Success Criteria:** Same result each time, no duplicates created

---

## Definition of Done

- [ ] BaselineRolesSeed.json configuration created
- [ ] IBaselineSeedService interface and implementation created
- [ ] Service registered in DI container
- [ ] Startup hook configured to run seed on deployment
- [ ] SeedController with management endpoints created
- [ ] All 6 baseline roles created in test environment
- [ ] All 4 SoD rules seeded successfully
- [ ] Idempotency verified (3 consecutive runs)
- [ ] Unit tests pass (100% coverage for seed service)
- [ ] Integration verification completed (5 checkpoints)
- [ ] Audit events logged for all seed operations
- [ ] Documentation updated (deployment guide)
- [ ] Code review completed
- [ ] PR merged to feature branch

---

## Dependencies

**Upstream Dependencies:**
- Story 1.1 (Database Schema Extensions) - requires SoDRules table

**Downstream Dependencies:**
- Story 1.10 (SoD Enforcement) - uses seeded SoD rules
- Story 1.12 (Migration) - baseline roles needed for user migration

---

## Notes for Developers

### Environment Configuration

**appsettings.json:**

```json
{
  "SeedBaselineData": true,
  "BaselineSeedOptions": {
    "ConfigurationPath": "Data/Seeds/BaselineRolesSeed.json",
    "SkipExisting": true,
    "RequireTransaction": true
  }
}
```

### Common Issues

**Issue 1:** Configuration file not found
- **Solution:** Ensure BaselineRolesSeed.json is copied to output directory (set Copy to Output Directory = Always in .csproj)

**Issue 2:** Permission conflicts with existing roles
- **Solution:** Seed service skips existing roles - this is expected behavior

**Issue 3:** Transaction rollback on audit service failure
- **Solution:** Ensure Admin Service is accessible for audit event forwarding

### Tips

- Use environment variable `SEED_BASELINE_DATA=true` for container deployments
- Customize seed data per environment by using different JSON files
- Monitor seed execution time in production (should be <10 seconds)
- Consider feature flag for controlled rollout: `FeatureManagement:BaselineSeed`

---

**END OF STORY 1.7**
