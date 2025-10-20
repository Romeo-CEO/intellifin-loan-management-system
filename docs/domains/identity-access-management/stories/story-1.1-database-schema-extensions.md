# Story 1.1: Database Schema Extensions

## Story Information

**Epic:** Foundation Setup (Epic 1)  
**Story ID:** 1.1  
**Story Name:** Database Schema Extensions  
**Priority:** Critical  
**Estimated Effort:** 5 story points (8-12 hours)  
**Dependencies:** None (first story in sequence)  
**Blocks:** Stories 1.5, 2.1, 3.1, 4.1 (all data-dependent stories)

---

## Story Description

As a **Backend Developer**, I want to **extend the database schema with new IAM tables** so that **the system can support multi-tenancy, service accounts, SoD rules, and enhanced audit logging** without impacting existing functionality.

### Business Value

- Enables all downstream IAM features (tenancy, service accounts, SoD)
- Establishes foundation for gradual Keycloak migration
- Maintains backward compatibility with existing data
- Provides audit trail for compliance requirements

### User Story

```
Given the existing LmsDbContext with ASP.NET Identity tables
When I add EF Core migrations for 8 new IAM tables
Then the database schema should be extended without modifying existing tables
And all existing queries should continue to work
And the migration should be idempotent and rollback-safe
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** 8 new tables created via EF Core migration:
  - Tenants
  - TenantUsers
  - TenantBranches
  - ServiceAccounts
  - ServiceCredentials
  - SoDRules
  - AuditEvents
  - TokenRevocations

- [ ] **AC2:** All foreign key relationships established correctly:
  - TenantUsers.TenantId → Tenants.TenantId (CASCADE)
  - TenantUsers.UserId → AspNetUsers.Id (CASCADE)
  - TenantBranches.TenantId → Tenants.TenantId (CASCADE)
  - ServiceCredentials.ServiceAccountId → ServiceAccounts.ServiceAccountId (CASCADE)

- [ ] **AC3:** All indexes created for query optimization:
  - IX_TenantUsers_UserId (lookup user's tenants)
  - IX_ServiceAccounts_ClientId (service authentication)
  - IX_AuditEvents_Timestamp (time-range queries)
  - IX_AuditEvents_ActorId (user audit history)
  - IX_AuditEvents_TenantId (filtered index, WHERE TenantId IS NOT NULL)
  - IX_TokenRevocations_TokenId (unique, revocation check)
  - IX_TokenRevocations_ExpiresAt (cleanup job)

- [ ] **AC4:** DbContext extended with new DbSet properties

- [ ] **AC5:** Seed migration created for baseline SoD rules

### Non-Functional Criteria

- [ ] **AC6:** Migration completes in <30 seconds on production-size database (100K+ users)

- [ ] **AC7:** Zero impact to existing tables (no ALTER TABLE statements on existing tables)

- [ ] **AC8:** Migration is idempotent (safe to run multiple times)

- [ ] **AC9:** Rollback (Down) migration successfully reverts all changes

- [ ] **AC10:** All new columns have appropriate default values (no breaking changes)

---

## Technical Specification

### Database Schema Details

#### Table 1: Tenants

```sql
CREATE TABLE [dbo].[Tenants] (
    [TenantId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Name] NVARCHAR(200) NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [Settings] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([TenantId])
);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_Code] ON [dbo].[Tenants] ([Code]);
```

#### Table 2: TenantUsers

```sql
CREATE TABLE [dbo].[TenantUsers] (
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    [Role] NVARCHAR(100) NULL,
    [AssignedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [AssignedBy] NVARCHAR(450) NULL,
    CONSTRAINT [PK_TenantUsers] PRIMARY KEY ([TenantId], [UserId]),
    CONSTRAINT [FK_TenantUsers_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([TenantId]) ON DELETE CASCADE,
    CONSTRAINT [FK_TenantUsers_AspNetUsers] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX [IX_TenantUsers_UserId] ON [dbo].[TenantUsers] ([UserId]);
```

#### Table 3: TenantBranches

```sql
CREATE TABLE [dbo].[TenantBranches] (
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [BranchId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_TenantBranches] PRIMARY KEY ([TenantId], [BranchId]),
    CONSTRAINT [FK_TenantBranches_Tenants] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants]([TenantId]) ON DELETE CASCADE
);
```

#### Table 4: ServiceAccounts

```sql
CREATE TABLE [dbo].[ServiceAccounts] (
    [ServiceAccountId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ClientId] NVARCHAR(100) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] NVARCHAR(450) NULL,
    CONSTRAINT [PK_ServiceAccounts] PRIMARY KEY ([ServiceAccountId])
);

CREATE UNIQUE NONCLUSTERED INDEX [IX_ServiceAccounts_ClientId] ON [dbo].[ServiceAccounts] ([ClientId]);
```

#### Table 5: ServiceCredentials

```sql
CREATE TABLE [dbo].[ServiceCredentials] (
    [CredentialId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ServiceAccountId] UNIQUEIDENTIFIER NOT NULL,
    [SecretHash] NVARCHAR(500) NOT NULL,
    [ExpiresAt] DATETIME2 NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [RevokedAt] DATETIME2 NULL,
    CONSTRAINT [PK_ServiceCredentials] PRIMARY KEY ([CredentialId]),
    CONSTRAINT [FK_ServiceCredentials_ServiceAccounts] FOREIGN KEY ([ServiceAccountId]) REFERENCES [dbo].[ServiceAccounts]([ServiceAccountId]) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX [IX_ServiceCredentials_ServiceAccountId] ON [dbo].[ServiceCredentials] ([ServiceAccountId]);
```

#### Table 6: SoDRules

```sql
CREATE TABLE [dbo].[SoDRules] (
    [RuleId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [RuleName] NVARCHAR(100) NOT NULL,
    [ConflictingPermissions] NVARCHAR(MAX) NOT NULL,
    [Enforcement] NVARCHAR(20) NOT NULL DEFAULT 'strict',
    [IsActive] BIT NOT NULL DEFAULT 1,
    [Description] NVARCHAR(500) NULL,
    CONSTRAINT [PK_SoDRules] PRIMARY KEY ([RuleId])
);

CREATE UNIQUE NONCLUSTERED INDEX [IX_SoDRules_RuleName] ON [dbo].[SoDRules] ([RuleName]);
```

#### Table 7: AuditEvents

```sql
CREATE TABLE [dbo].[AuditEvents] (
    [EventId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [ActorId] NVARCHAR(450) NOT NULL,
    [Action] NVARCHAR(100) NOT NULL,
    [Entity] NVARCHAR(100) NOT NULL,
    [EntityId] NVARCHAR(450) NOT NULL,
    [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [IpAddress] NVARCHAR(50) NULL,
    [Details] NVARCHAR(MAX) NULL,
    [BranchId] UNIQUEIDENTIFIER NULL,
    [TenantId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([EventId])
);

CREATE NONCLUSTERED INDEX [IX_AuditEvents_Timestamp] ON [dbo].[AuditEvents] ([Timestamp] DESC);
CREATE NONCLUSTERED INDEX [IX_AuditEvents_ActorId] ON [dbo].[AuditEvents] ([ActorId]);
CREATE NONCLUSTERED INDEX [IX_AuditEvents_TenantId] ON [dbo].[AuditEvents] ([TenantId]) WHERE [TenantId] IS NOT NULL;
```

#### Table 8: TokenRevocations

```sql
CREATE TABLE [dbo].[TokenRevocations] (
    [RevocationId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [TokenId] NVARCHAR(100) NOT NULL,
    [UserId] NVARCHAR(450) NOT NULL,
    [RevokedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [RevokedBy] NVARCHAR(450) NULL,
    [Reason] NVARCHAR(200) NULL,
    [ExpiresAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_TokenRevocations] PRIMARY KEY ([RevocationId])
);

CREATE UNIQUE NONCLUSTERED INDEX [IX_TokenRevocations_TokenId] ON [dbo].[TokenRevocations] ([TokenId]);
CREATE NONCLUSTERED INDEX [IX_TokenRevocations_ExpiresAt] ON [dbo].[TokenRevocations] ([ExpiresAt]);
```

---

## Implementation Steps

### Step 1: Create Domain Models

**Location:** `IntelliFin.IdentityService/Models/Domain/`

**Files to Create:**

1. **Tenant.cs**
2. **TenantUser.cs**
3. **TenantBranch.cs**
4. **ServiceAccount.cs**
5. **ServiceCredential.cs**
6. **SoDRule.cs**
7. **AuditEvent.cs**
8. **TokenRevocation.cs**

**Sample Implementation (Tenant.cs):**

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IntelliFin.IdentityService.Models.Domain;

/// <summary>
/// Represents an organizational tenant in the multi-tenant system
/// </summary>
[Table("Tenants")]
public class Tenant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON blob for tenant-specific settings (branding, features, policies)
    /// </summary>
    public string? Settings { get; set; }

    // Navigation properties
    public virtual ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    public virtual ICollection<TenantBranch> TenantBranches { get; set; } = new List<TenantBranch>();
}
```

### Step 2: Extend LmsDbContext

**Location:** `IntelliFin.IdentityService/Data/LmsDbContext.cs`

**Modification Required:**

```csharp
// Add new DbSet properties
public DbSet<Tenant> Tenants { get; set; } = null!;
public DbSet<TenantUser> TenantUsers { get; set; } = null!;
public DbSet<TenantBranch> TenantBranches { get; set; } = null!;
public DbSet<ServiceAccount> ServiceAccounts { get; set; } = null!;
public DbSet<ServiceCredential> ServiceCredentials { get; set; } = null!;
public DbSet<SoDRule> SoDRules { get; set; } = null!;
public DbSet<AuditEvent> AuditEvents { get; set; } = null!;
public DbSet<TokenRevocation> TokenRevocations { get; set; } = null!;

// Update OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure composite keys
    modelBuilder.Entity<TenantUser>()
        .HasKey(tu => new { tu.TenantId, tu.UserId });

    modelBuilder.Entity<TenantBranch>()
        .HasKey(tb => new { tb.TenantId, tb.BranchId });

    // Configure indexes
    modelBuilder.Entity<Tenant>()
        .HasIndex(t => t.Code)
        .IsUnique();

    modelBuilder.Entity<TenantUser>()
        .HasIndex(tu => tu.UserId);

    modelBuilder.Entity<ServiceAccount>()
        .HasIndex(sa => sa.ClientId)
        .IsUnique();

    modelBuilder.Entity<SoDRule>()
        .HasIndex(sr => sr.RuleName)
        .IsUnique();

    modelBuilder.Entity<AuditEvent>()
        .HasIndex(ae => ae.Timestamp)
        .IsDescending();

    modelBuilder.Entity<AuditEvent>()
        .HasIndex(ae => ae.ActorId);

    modelBuilder.Entity<AuditEvent>()
        .HasIndex(ae => ae.TenantId)
        .HasFilter("[TenantId] IS NOT NULL");

    modelBuilder.Entity<TokenRevocation>()
        .HasIndex(tr => tr.TokenId)
        .IsUnique();

    modelBuilder.Entity<TokenRevocation>()
        .HasIndex(tr => tr.ExpiresAt);

    // Configure relationships
    modelBuilder.Entity<TenantUser>()
        .HasOne<Tenant>()
        .WithMany(t => t.TenantUsers)
        .HasForeignKey(tu => tu.TenantId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<TenantBranch>()
        .HasOne<Tenant>()
        .WithMany(t => t.TenantBranches)
        .HasForeignKey(tb => tb.TenantId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<ServiceCredential>()
        .HasOne<ServiceAccount>()
        .WithMany()
        .HasForeignKey(sc => sc.ServiceAccountId)
        .OnDelete(DeleteBehavior.Cascade);
}
```

### Step 3: Generate EF Core Migration

**Command:**

```powershell
cd "IntelliFin.IdentityService"
dotnet ef migrations add IAMEnhancement_SchemaExtensions --context LmsDbContext --output-dir Data/Migrations
```

**Expected Output:**
- Migration file: `Data/Migrations/20251015XXXXXX_IAMEnhancement_SchemaExtensions.cs`
- ModelSnapshot file: `Data/Migrations/LmsDbContextModelSnapshot.cs` (updated)

### Step 4: Create Seed Data Migration

**Location:** `IntelliFin.IdentityService/Data/Seeds/SoDRulesSeed.cs`

**Content:**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using IntelliFin.IdentityService.Models.Domain;

namespace IntelliFin.IdentityService.Data.Seeds;

/// <summary>
/// Seed baseline Separation of Duties rules
/// </summary>
public static class SoDRulesSeed
{
    public static void ApplySoDRulesSeed(MigrationBuilder migrationBuilder)
    {
        var rules = new[]
        {
            new
            {
                RuleId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                RuleName = "sod-loan-create-approve",
                ConflictingPermissions = "[\"loans:create\", \"loans:approve\"]",
                Enforcement = "strict",
                IsActive = true,
                Description = "Prevent same user from creating and approving loans"
            },
            new
            {
                RuleId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                RuleName = "sod-disbursement-approval",
                ConflictingPermissions = "[\"loans:disburse\", \"loans:approve\"]",
                Enforcement = "strict",
                IsActive = true,
                Description = "Prevent same user from approving and disbursing loans"
            },
            new
            {
                RuleId = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                RuleName = "sod-user-admin-operations",
                ConflictingPermissions = "[\"users:create\", \"roles:assign\"]",
                Enforcement = "warning",
                IsActive = true,
                Description = "Warn when same user can create users and assign roles"
            }
        };

        foreach (var rule in rules)
        {
            migrationBuilder.InsertData(
                table: "SoDRules",
                columns: new[] { "RuleId", "RuleName", "ConflictingPermissions", "Enforcement", "IsActive", "Description" },
                values: new object[] { rule.RuleId, rule.RuleName, rule.ConflictingPermissions, rule.Enforcement, rule.IsActive, rule.Description }
            );
        }
    }
}
```

**Generate seed migration:**

```powershell
dotnet ef migrations add IAMEnhancement_SoDRulesSeed --context LmsDbContext --output-dir Data/Migrations
```

### Step 5: Verify Migration

**Command:**

```powershell
dotnet ef migrations script --context LmsDbContext --output migration-script.sql
```

**Review Checklist:**
- [ ] No ALTER TABLE on existing tables (AspNetUsers, AspNetRoles, etc.)
- [ ] All CREATE TABLE statements have IF NOT EXISTS checks (idempotent)
- [ ] All indexes created with appropriate names
- [ ] Foreign keys have CASCADE delete behavior
- [ ] Default values specified for all required columns

---

## Testing Requirements

### Unit Tests

**Location:** `IntelliFin.IdentityService.Tests/Data/`

**Test File:** `LmsDbContextTests.cs`

**Test Cases:**

```csharp
[Fact]
public void DbContext_HasTenantsDbSet()
{
    // Arrange
    var options = CreateInMemoryDbContextOptions();
    
    // Act
    using var context = new LmsDbContext(options);
    
    // Assert
    context.Tenants.Should().NotBeNull();
}

[Fact]
public async Task Tenant_CanBeCreatedAndRetrieved()
{
    // Arrange
    var options = CreateInMemoryDbContextOptions();
    using var context = new LmsDbContext(options);
    
    var tenant = new Tenant
    {
        Name = "Test Tenant",
        Code = "test-tenant",
        IsActive = true
    };
    
    // Act
    context.Tenants.Add(tenant);
    await context.SaveChangesAsync();
    
    var retrieved = await context.Tenants.FirstOrDefaultAsync(t => t.Code == "test-tenant");
    
    // Assert
    retrieved.Should().NotBeNull();
    retrieved!.Name.Should().Be("Test Tenant");
}

[Fact]
public async Task TenantUser_CascadeDeletesWhenTenantDeleted()
{
    // Arrange
    var options = CreateInMemoryDbContextOptions();
    using var context = new LmsDbContext(options);
    
    var tenant = new Tenant { Name = "Test", Code = "test" };
    var user = new ApplicationUser { UserName = "testuser", Email = "test@test.com" };
    context.Tenants.Add(tenant);
    context.Users.Add(user);
    await context.SaveChangesAsync();
    
    var tenantUser = new TenantUser { TenantId = tenant.TenantId, UserId = user.Id };
    context.TenantUsers.Add(tenantUser);
    await context.SaveChangesAsync();
    
    // Act
    context.Tenants.Remove(tenant);
    await context.SaveChangesAsync();
    
    // Assert
    var orphanedTenantUser = await context.TenantUsers
        .FirstOrDefaultAsync(tu => tu.TenantId == tenant.TenantId);
    orphanedTenantUser.Should().BeNull();
}
```

### Integration Tests

**Test Migration Execution:**

```powershell
# Test migration on local development database
dotnet ef database update --context LmsDbContext --connection "Server=localhost;Database=IntelliFin_Test;Trusted_Connection=True;"

# Verify tables created
sqlcmd -S localhost -d IntelliFin_Test -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME IN ('Tenants', 'TenantUsers', 'ServiceAccounts', 'SoDRules', 'AuditEvents', 'TokenRevocations')"

# Test rollback
dotnet ef database update 0 --context LmsDbContext --connection "Server=localhost;Database=IntelliFin_Test;Trusted_Connection=True;"
```

### Performance Tests

**Query Performance Verification:**

```sql
-- Test tenant lookup by code (should use unique index)
SET STATISTICS IO ON;
SELECT * FROM Tenants WHERE Code = 'test-tenant';
-- Expected: Index Seek on IX_Tenants_Code

-- Test user's tenants lookup (should use index)
SELECT t.* FROM Tenants t
INNER JOIN TenantUsers tu ON t.TenantId = tu.TenantId
WHERE tu.UserId = '550e8400-e29b-41d4-a716-446655440000';
-- Expected: Index Seek on IX_TenantUsers_UserId

-- Test audit event time-range query (should use descending index)
SELECT * FROM AuditEvents
WHERE Timestamp >= DATEADD(day, -7, GETUTCDATE())
ORDER BY Timestamp DESC;
-- Expected: Index Seek on IX_AuditEvents_Timestamp
```

---

## Integration Verification

### Checkpoint 1: Existing Tables Untouched

**Verification:**

```sql
-- Check no modifications to AspNetUsers table
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AspNetUsers'
ORDER BY ORDINAL_POSITION;

-- Compare with baseline schema (should be identical)
```

**Success Criteria:** No differences detected

### Checkpoint 2: All New Tables Created

**Verification:**

```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('Tenants', 'TenantUsers', 'TenantBranches', 'ServiceAccounts', 'ServiceCredentials', 'SoDRules', 'AuditEvents', 'TokenRevocations')
ORDER BY TABLE_NAME;
```

**Success Criteria:** All 8 tables returned

### Checkpoint 3: Foreign Key Constraints Valid

**Verification:**

```sql
SELECT 
    fk.name AS ForeignKeyName,
    tp.name AS ParentTable,
    cp.name AS ParentColumn,
    tr.name AS ReferencedTable,
    cr.name AS ReferencedColumn,
    fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tp.name IN ('TenantUsers', 'TenantBranches', 'ServiceCredentials');
```

**Success Criteria:** All FKs have CASCADE delete action

### Checkpoint 4: Indexes Exist and Optimal

**Verification:**

```sql
SELECT 
    i.name AS IndexName,
    t.name AS TableName,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE t.name IN ('Tenants', 'TenantUsers', 'ServiceAccounts', 'AuditEvents', 'TokenRevocations')
ORDER BY t.name, i.name;
```

**Success Criteria:** All 10 indexes present with correct uniqueness

### Checkpoint 5: Existing Unit Tests Pass

**Verification:**

```powershell
cd IntelliFin.IdentityService.Tests
dotnet test --filter "FullyQualifiedName~IntelliFin.IdentityService.Tests" --logger "console;verbosity=detailed"
```

**Success Criteria:** All existing tests pass (0 failures)

---

## Rollback Plan

### Automatic Rollback (EF Core)

```powershell
# Rollback to previous migration
dotnet ef database update <PreviousMigrationName> --context LmsDbContext

# Complete rollback (remove all migrations)
dotnet ef database update 0 --context LmsDbContext
```

### Manual Rollback (SQL Script)

```sql
-- Drop all new tables in reverse dependency order
DROP TABLE IF EXISTS [dbo].[TenantUsers];
DROP TABLE IF EXISTS [dbo].[TenantBranches];
DROP TABLE IF EXISTS [dbo].[ServiceCredentials];
DROP TABLE IF EXISTS [dbo].[TokenRevocations];
DROP TABLE IF EXISTS [dbo].[AuditEvents];
DROP TABLE IF EXISTS [dbo].[Tenants];
DROP TABLE IF EXISTS [dbo].[ServiceAccounts];
DROP TABLE IF EXISTS [dbo].[SoDRules];
```

### Verification After Rollback

```sql
-- Verify no orphaned tables
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('Tenants', 'TenantUsers', 'TenantBranches', 'ServiceAccounts', 'ServiceCredentials', 'SoDRules', 'AuditEvents', 'TokenRevocations');
-- Expected: 0 rows
```

---

## Definition of Done

- [ ] All 8 domain models created in `Models/Domain/`
- [ ] LmsDbContext extended with new DbSet properties
- [ ] EF Core migration generated and reviewed
- [ ] Seed data migration created for SoD rules
- [ ] Migration applied successfully to development database
- [ ] All unit tests pass (existing + new)
- [ ] Integration verification completed (5 checkpoints)
- [ ] Performance tests show no regression
- [ ] Rollback tested and verified
- [ ] Code review completed
- [ ] Documentation updated (database schema diagram)
- [ ] PR merged to `feature/iam-enhancement` branch

---

## Dependencies

**Upstream Dependencies:** None (first story)

**Downstream Dependencies:**
- Story 1.5 (User Provisioning) - needs Tenant, ServiceAccount tables
- Story 2.1 (Tenant Management Service) - needs Tenant, TenantUser tables
- Story 3.1 (Service Account Management) - needs ServiceAccount, ServiceCredential tables
- Story 4.1 (SoD Validation Service) - needs SoDRules table

---

## Notes for Developers

### Common Issues

**Issue 1:** Migration fails with "object already exists"
- **Solution:** Run `dotnet ef database update 0` to rollback, then reapply

**Issue 2:** Foreign key constraint violation during testing
- **Solution:** Ensure test data created in correct order (parent before child)

**Issue 3:** EF Core In-Memory provider doesn't enforce FKs
- **Solution:** Use SQLite in-memory mode for integration tests that require FK validation

### Tips

- Use `dotnet ef migrations script --idempotent` to generate production-safe migration scripts
- Always test rollback before deploying migration
- Monitor migration execution time on production-like dataset (100K+ users)
- Keep migrations small and focused (this migration only creates tables, no data manipulation)

---

**END OF STORY 1.1**
