# Story 1.2: ASP.NET Core Identity User Migration to Keycloak

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.2 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 1: Foundation |
| **Sprint** | Sprint 1 |
| **Story Points** | 8 |
| **Estimated Effort** | 5-7 days |
| **Priority** | P0 (Blocker for JWT migration) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Story 1.1 (Keycloak deployed) |
| **Blocks** | Stories 1.3, 1.5 |

---

## User Story

**As a** System Administrator,  
**I want** existing users, roles, and permissions migrated from ASP.NET Core Identity to Keycloak,  
**so that** we preserve user access and avoid forced re-login/re-registration.

---

## Business Value

User migration is critical for seamless transition to the new Identity Provider without disrupting business operations:

- **Zero User Impact**: Existing users retain access without re-registration
- **Preserve Authorization**: All role assignments and permissions migrated intact
- **Business Continuity**: No forced password resets or account lockouts
- **Audit Trail Continuity**: User IDs mapped for historical audit trail integrity
- **Regulatory Compliance**: Maintain 7-year audit records with valid user references

This migration enables the brownfield transition strategy, allowing gradual cutover without business disruption.

---

## Acceptance Criteria

### AC1: User Data Extraction
**Given** ASP.NET Core Identity database is accessible  
**When** ETL script executes  
**Then**:
- All users extracted from `AspNetUsers` table
- User attributes captured: Username, Email, FirstName, LastName, PhoneNumber, BranchId, TenantId
- User status preserved: EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled
- Extraction validation report shows user count matches source database

### AC2: Keycloak User Import
**Given** Users extracted from ASP.NET Core Identity  
**When** importing to Keycloak via Admin API  
**Then**:
- Users created in IntelliFin realm with Keycloak UUIDs
- User attributes mapped to Keycloak user attributes
- User ID mapping table created in Admin Service database: `AspNetUserId → KeycloakUserId`
- Email verification status preserved
- First login forces password reset (security best practice)

### AC3: Role Migration
**Given** Roles exist in `AspNetRoles` table  
**When** migrating to Keycloak  
**Then**:
- All roles created as Keycloak realm roles
- Role descriptions preserved
- Role hierarchy maintained (if exists in custom claims)
- Role ID mapping table created: `AspNetRoleId → KeycloakRoleId`

### AC4: User-Role Assignment Migration
**Given** User-role mappings exist in `AspNetUserRoles` table  
**When** migrating role assignments  
**Then**:
- All user-role assignments recreated in Keycloak
- Composite roles mapped correctly
- Role assignments validated via Keycloak Admin API query

### AC5: Migration Validation
**Given** Migration completed  
**When** validation script runs  
**Then**:
- User count matches: Source DB = Keycloak realm
- Role count matches: Source DB = Keycloak realm
- User-role assignment count matches
- Sample user queries validated (10% random sample)
- Migration validation report generated (success/failure status)

### AC6: Rollback Capability
**Given** Migration issues detected  
**When** rollback script executes  
**Then**:
- Keycloak users deleted
- ASP.NET Core Identity database remains intact (read-only)
- IdentityService reverts to ASP.NET Core Identity authentication
- Rollback completion logged in audit trail

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Section 5.1 (Stories), Phase 1 (Lines 619-642)  
**Architecture Sections**: Section 4.2 (Keycloak Identity Provider)  
**Requirements**: FR2, CR1, CR2

### Technology Stack

- **Source Database**: SQL Server 2022 (ASP.NET Core Identity schema)
- **Target IdP**: Keycloak 24+ Admin REST API
- **ETL Tool**: C# console application (.NET 9)
- **Libraries**: Keycloak.AuthServices.Sdk, Entity Framework Core
- **Validation**: SQL queries + Keycloak Admin API

### Implementation Tasks

#### Task 1: User Mapping Database Schema

```sql
-- Admin Service database
CREATE TABLE UserIdMapping (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AspNetUserId NVARCHAR(450) NOT NULL UNIQUE,
    KeycloakUserId NVARCHAR(100) NOT NULL UNIQUE,
    MigrationDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    MigrationStatus NVARCHAR(50) NOT NULL,  -- 'Completed', 'Failed', 'Pending'
    INDEX IX_AspNetUserId (AspNetUserId),
    INDEX IX_KeycloakUserId (KeycloakUserId)
);

CREATE TABLE RoleIdMapping (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AspNetRoleId NVARCHAR(450) NOT NULL UNIQUE,
    KeycloakRoleId NVARCHAR(100) NOT NULL UNIQUE,
    RoleName NVARCHAR(256) NOT NULL,
    MigrationDate DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

#### Task 2: ETL Script Structure

```csharp
public class UserMigrationService
{
    private readonly IdentityDbContext _identityDb;
    private readonly KeycloakClient _keycloakClient;
    private readonly AdminDbContext _adminDb;
    
    public async Task<MigrationResult> MigrateUsersAsync()
    {
        var users = await _identityDb.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();
        
        var result = new MigrationResult();
        
        foreach (var user in users)
        {
            try
            {
                var keycloakUser = MapToKeycloakUser(user);
                var keycloakUserId = await _keycloakClient.CreateUserAsync(keycloakUser);
                
                // Create mapping
                await _adminDb.UserIdMappings.AddAsync(new UserIdMapping
                {
                    AspNetUserId = user.Id,
                    KeycloakUserId = keycloakUserId,
                    MigrationStatus = "Completed"
                });
                
                // Migrate role assignments
                await MigrateUserRolesAsync(user, keycloakUserId);
                
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailedUsers.Add(new FailedMigration 
                { 
                    UserId = user.Id, 
                    Email = user.Email, 
                    Error = ex.Message 
                });
            }
        }
        
        await _adminDb.SaveChangesAsync();
        return result;
    }
    
    private KeycloakUserRepresentation MapToKeycloakUser(IdentityUser user)
    {
        return new KeycloakUserRepresentation
        {
            Username = user.UserName,
            Email = user.Email,
            EmailVerified = user.EmailConfirmed,
            Enabled = !user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow,
            Attributes = new Dictionary<string, List<string>>
            {
                ["firstName"] = new List<string> { user.FirstName },
                ["lastName"] = new List<string> { user.LastName },
                ["branchId"] = new List<string> { user.BranchId?.ToString() },
                ["tenantId"] = new List<string> { user.TenantId?.ToString() },
                ["phoneNumber"] = new List<string> { user.PhoneNumber }
            },
            // Force password reset on first login
            RequiredActions = new List<string> { "UPDATE_PASSWORD" }
        };
    }
}
```

#### Task 3: Role Migration Script

```csharp
public async Task MigrateRolesAsync()
{
    var roles = await _identityDb.Roles.ToListAsync();
    
    foreach (var role in roles)
    {
        var keycloakRole = new RoleRepresentation
        {
            Name = role.Name,
            Description = role.Name // Can enhance with custom description
        };
        
        await _keycloakClient.CreateRealmRoleAsync("IntelliFin", keycloakRole);
        
        // Get created role ID
        var createdRole = await _keycloakClient.GetRealmRoleByNameAsync("IntelliFin", role.Name);
        
        await _adminDb.RoleIdMappings.AddAsync(new RoleIdMapping
        {
            AspNetRoleId = role.Id,
            KeycloakRoleId = createdRole.Id,
            RoleName = role.Name
        });
    }
    
    await _adminDb.SaveChangesAsync();
}
```

#### Task 4: Validation Script

```csharp
public async Task<ValidationResult> ValidateMigrationAsync()
{
    var result = new ValidationResult();
    
    // Validate user count
    var aspNetUserCount = await _identityDb.Users.CountAsync();
    var keycloakUserCount = await _keycloakClient.GetRealmUsersCountAsync("IntelliFin");
    result.UserCountMatch = aspNetUserCount == keycloakUserCount;
    
    // Validate role count
    var aspNetRoleCount = await _identityDb.Roles.CountAsync();
    var keycloakRoles = await _keycloakClient.GetRealmRolesAsync("IntelliFin");
    result.RoleCountMatch = aspNetRoleCount == keycloakRoles.Count;
    
    // Sample validation (10% random users)
    var sampleUsers = await _identityDb.Users
        .OrderBy(u => Guid.NewGuid())
        .Take((int)(aspNetUserCount * 0.1))
        .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
        .ToListAsync();
    
    foreach (var user in sampleUsers)
    {
        var mapping = await _adminDb.UserIdMappings
            .FirstOrDefaultAsync(m => m.AspNetUserId == user.Id);
        
        if (mapping == null)
        {
            result.SampleErrors.Add($"User {user.Email} not found in mapping table");
            continue;
        }
        
        var keycloakUser = await _keycloakClient.GetUserByIdAsync("IntelliFin", mapping.KeycloakUserId);
        
        if (keycloakUser == null)
        {
            result.SampleErrors.Add($"User {user.Email} not found in Keycloak");
            continue;
        }
        
        // Validate role assignments
        var aspNetRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var keycloakRoleMappings = await _keycloakClient.GetUserRealmRoleMappingsAsync("IntelliFin", mapping.KeycloakUserId);
        var keycloakRoles = keycloakRoleMappings.Select(r => r.Name).ToList();
        
        if (!aspNetRoles.OrderBy(r => r).SequenceEqual(keycloakRoles.OrderBy(r => r)))
        {
            result.SampleErrors.Add($"User {user.Email} role mismatch: ASP.NET={string.Join(",", aspNetRoles)}, Keycloak={string.Join(",", keycloakRoles)}");
        }
    }
    
    result.IsValid = result.UserCountMatch && result.RoleCountMatch && result.SampleErrors.Count == 0;
    return result;
}
```

#### Task 5: Rollback Script

```csharp
public async Task RollbackMigrationAsync()
{
    // Delete all users from Keycloak IntelliFin realm
    var keycloakUsers = await _keycloakClient.GetRealmUsersAsync("IntelliFin");
    
    foreach (var user in keycloakUsers)
    {
        await _keycloakClient.DeleteUserAsync("IntelliFin", user.Id);
    }
    
    // Delete all realm roles (except default Keycloak roles)
    var keycloakRoles = await _keycloakClient.GetRealmRolesAsync("IntelliFin");
    foreach (var role in keycloakRoles.Where(r => !r.IsComposite && r.ClientRole == false))
    {
        await _keycloakClient.DeleteRealmRoleAsync("IntelliFin", role.Name);
    }
    
    // Clear mapping tables
    _adminDb.UserIdMappings.RemoveRange(_adminDb.UserIdMappings);
    _adminDb.RoleIdMappings.RemoveRange(_adminDb.RoleIdMappings);
    await _adminDb.SaveChangesAsync();
    
    // ASP.NET Core Identity database remains intact - no changes needed
}
```

---

## Integration Verification

### IV1: ASP.NET Core Identity Database Intact
**Verification Steps**:
1. Verify ASP.NET Core Identity database schema unchanged
2. Confirm all tables remain populated
3. Test IdentityService authentication still functional with old database

**Success Criteria**: ASP.NET Core Identity database in read-only state for 90-day safety period.

### IV2: User ID Mapping Table Functional
**Verification Steps**:
1. Query `UserIdMapping` table for sample users
2. Verify mapping bidirectional: `AspNetUserId ↔ KeycloakUserId`
3. Test foreign key compatibility: Business table `CreatedBy` fields resolve correctly

**Success Criteria**: All foreign key relationships in business tables (LoanApplications, Clients, etc.) remain valid.

### IV3: Historical Audit Trail Integrity
**Verification Steps**:
1. Query existing audit events with `AspNetUserId`
2. Use `UserIdMapping` to resolve to `KeycloakUserId`
3. Verify audit trail queries return correct user information

**Success Criteria**: Historical audit queries work with user ID mapping, no broken references.

---

## Testing Strategy

### Unit Tests
1. **MapToKeycloakUser Test**
   - Test ASP.NET Core Identity user converts to Keycloak representation
   - Verify all attributes mapped correctly
   - Test edge cases: null phone numbers, disabled users

2. **Validation Logic Test**
   - Test user count validation
   - Test role assignment validation
   - Test error detection for missing mappings

### Integration Tests
1. **End-to-End Migration Test**
   - Run full migration on test database (100 users)
   - Verify all users, roles, assignments migrated
   - Run validation script (should pass)

2. **Rollback Test**
   - Run migration on test environment
   - Execute rollback script
   - Verify Keycloak users deleted
   - Verify mapping tables cleared

### Performance Tests
- **Large-Scale Migration**: Test migration of 5,000 users
  - Target: <2 hours total migration time
  - Target: <500ms per user (Keycloak API call + DB insert)

### Data Integrity Tests
1. **Sample Validation**
   - Validate 10% random sample post-migration
   - Test role assignment accuracy
   - Test user attribute integrity

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Keycloak API rate limiting causes migration timeout | High | Medium | Implement batch processing with rate limiting, retry logic with exponential backoff |
| User ID mapping breaks foreign key references in business tables | High | Low | Extensive pre-migration testing, mapping table with bidirectional indexes |
| Password reset requirement causes user friction | Medium | High | User communication campaign, password reset help desk support |
| Migration failure leaves system in inconsistent state | High | Low | Transaction boundaries, rollback script, read-only ASP.NET DB for 90 days |

---

## Definition of Done (DoD)

- [ ] ETL script creates all users in Keycloak IntelliFin realm
- [ ] All roles migrated to Keycloak realm roles
- [ ] User-role assignments preserved
- [ ] User ID mapping table populated in Admin Service database
- [ ] Migration validation script passes (user count, role count, sample validation)
- [ ] Rollback script tested and documented
- [ ] Foreign key compatibility validated (business tables `CreatedBy`/`UpdatedBy` fields work)
- [ ] User communication sent: Password reset on first Keycloak login
- [ ] Migration report generated and stored in MinIO
- [ ] Code review completed
- [ ] Documentation updated in `docs/domains/system-administration/user-migration-guide.md`

---

## Related Documentation

### PRD References
- **Full PRD**: `../system-administration-control-plane-prd.md` (Lines 619-642)
- **Requirements**: FR2, CR1, CR2

### Architecture References
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (Section 4.2)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] ASP.NET Core Identity database backup completed
- [ ] Keycloak Admin API service account credentials configured in Vault
- [ ] Admin Service database schema deployed (UserIdMapping, RoleIdMapping tables)
- [ ] User communication prepared: Password reset email templates
- [ ] Rollback procedure reviewed with ops team

### Post-Implementation Handoff
- User helpdesk trained on password reset support
- Migration report reviewed with compliance team
- ASP.NET Core Identity database marked read-only (retain for 90 days)
- Monitoring alerts configured for migration validation failures

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: Story 1.3 - API Gateway Keycloak JWT Validation (Dual-Token Support)
