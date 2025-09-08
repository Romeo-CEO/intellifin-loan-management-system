using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Shared.DomainModels.Data;

public class LmsDbContext : DbContext
{
    public LmsDbContext(DbContextOptions<LmsDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<GLAccount> GLAccounts => Set<GLAccount>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    // Identity and Authorization
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    
    // Sprint 3 Entities
    public DbSet<CreditAssessment> CreditAssessments => Set<CreditAssessment>();
    public DbSet<CreditFactor> CreditFactors => Set<CreditFactor>();
    public DbSet<RiskIndicator> RiskIndicators => Set<RiskIndicator>();
    public DbSet<ApplicationField> ApplicationFields => Set<ApplicationField>();
    public DbSet<ValidationRule> ValidationRules => Set<ValidationRule>();
    public DbSet<GLEntry> GLEntries => Set<GLEntry>();
    public DbSet<GLEntryLine> GLEntryLines => Set<GLEntryLine>();
    public DbSet<GLBalance> GLBalances => Set<GLBalance>();
    
    // System-Assisted Manual Verification
    public DbSet<DocumentVerification> DocumentVerifications => Set<DocumentVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("Clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.Property(x => x.NationalId).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.NationalId).IsUnique();
            b.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<LoanApplication>(b =>
        {
            b.ToTable("LoanApplications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
            b.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)").IsRequired();
            b.Property(x => x.TermMonths).IsRequired();
            b.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProductName).HasMaxLength(200);
            b.Property(x => x.Status).HasMaxLength(32).IsRequired();
            b.Property(x => x.ApplicationDataJson).HasColumnType("nvarchar(max)");
            b.Property(x => x.WorkflowInstanceId).HasMaxLength(100);
            b.Property(x => x.DeclineReason).HasMaxLength(500);
            b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.HasOne(x => x.Client)
             .WithMany(c => c.LoanApplications)
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Product)
             .WithMany(p => p.LoanApplications)
             .HasForeignKey(x => x.ProductCode)
             .HasPrincipalKey(p => p.Code)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ClientId, x.CreatedAtUtc });
            b.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<LoanProduct>(b =>
        {
            b.ToTable("LoanProducts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Category).HasMaxLength(100);
            b.Property(x => x.MinAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.MaxAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.BaseInterestRate).HasColumnType("decimal(5,4)");
            b.Property(x => x.InterestRateAnnualPercent).HasColumnType("decimal(5,2)").IsRequired();
            b.Property(x => x.TermMonthsDefault).IsRequired();
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<GLAccount>(b =>
        {
            b.ToTable("GLAccounts");
            b.HasKey(x => x.Id);
            b.Property(x => x.AccountCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Category).HasMaxLength(50).IsRequired();
            b.Property(x => x.AccountType).HasMaxLength(50);
            b.Property(x => x.CurrentBalance).HasColumnType("decimal(18,2)");
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.HasOne(x => x.ParentAccount)
             .WithMany(p => p.SubAccounts)
             .HasForeignKey(x => x.ParentAccountId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.AccountCode).IsUnique();
            b.HasIndex(x => x.Category);
            b.HasIndex(x => x.ParentAccountId);
        });

        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.ToTable("AuditEvents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            b.Property(x => x.Action).HasMaxLength(200).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(200).IsRequired();
            b.Property(x => x.EntityId).HasMaxLength(200).IsRequired();
            b.Property(x => x.OccurredAtUtc).IsRequired();
            b.Property(x => x.Data);
            b.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
        });

        // Sprint 3 Entity Configurations
        modelBuilder.Entity<CreditAssessment>(b =>
        {
            b.ToTable("CreditAssessments");
            b.HasKey(x => x.Id);
            b.Property(x => x.RiskGrade).HasMaxLength(10).IsRequired();
            b.Property(x => x.CreditScore).HasColumnType("decimal(8,2)");
            b.Property(x => x.DebtToIncomeRatio).HasColumnType("decimal(5,4)");
            b.Property(x => x.PaymentCapacity).HasColumnType("decimal(18,2)");
            b.Property(x => x.ScoreExplanation).HasColumnType("nvarchar(max)");
            b.Property(x => x.AssessedBy).HasMaxLength(200);
            b.HasOne(x => x.LoanApplication)
             .WithMany(l => l.CreditAssessments)
             .HasForeignKey(x => x.LoanApplicationId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.LoanApplicationId);
            b.HasIndex(x => x.RiskGrade);
            b.HasIndex(x => x.AssessedAt);
        });

        modelBuilder.Entity<CreditFactor>(b =>
        {
            b.ToTable("CreditFactors");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Value).HasMaxLength(500);
            b.Property(x => x.Weight).HasColumnType("decimal(5,4)");
            b.Property(x => x.Score).HasColumnType("decimal(8,2)");
            b.Property(x => x.Impact).HasMaxLength(50);
            b.HasOne(x => x.CreditAssessment)
             .WithMany(c => c.CreditFactors)
             .HasForeignKey(x => x.CreditAssessmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RiskIndicator>(b =>
        {
            b.ToTable("RiskIndicators");
            b.HasKey(x => x.Id);
            b.Property(x => x.Category).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            b.Property(x => x.Level).HasMaxLength(50).IsRequired();
            b.Property(x => x.Impact).HasColumnType("decimal(8,2)");
            b.HasOne(x => x.CreditAssessment)
             .WithMany(c => c.RiskIndicators)
             .HasForeignKey(x => x.CreditAssessmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationField>(b =>
        {
            b.ToTable("ApplicationFields");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Label).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasMaxLength(50).IsRequired();
            b.Property(x => x.DefaultValue).HasMaxLength(500);
            b.Property(x => x.ValidationPattern).HasMaxLength(500);
            b.Property(x => x.HelpText).HasMaxLength(1000);
            b.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");
            b.HasOne(x => x.LoanProduct)
             .WithMany(p => p.RequiredFields)
             .HasForeignKey(x => x.LoanProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValidationRule>(b =>
        {
            b.ToTable("ValidationRules");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Expression).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.ErrorMessage).HasMaxLength(1000).IsRequired();
            b.Property(x => x.Category).HasMaxLength(100);
            b.HasOne(x => x.LoanProduct)
             .WithMany(p => p.ValidationRules)
             .HasForeignKey(x => x.LoanProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GLEntry>(b =>
        {
            b.ToTable("GLEntries");
            b.HasKey(x => x.Id);
            b.Property(x => x.EntryNumber).HasMaxLength(50).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Reference).HasMaxLength(200);
            b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Status).HasMaxLength(50);
            b.Property(x => x.CreatedBy).HasMaxLength(200);
            b.Property(x => x.BatchId).HasMaxLength(100);
            b.HasIndex(x => x.EntryNumber).IsUnique();
            b.HasIndex(x => x.TransactionDate);
            b.HasIndex(x => x.BatchId);
        });

        modelBuilder.Entity<GLEntryLine>(b =>
        {
            b.ToTable("GLEntryLines");
            b.HasKey(x => x.Id);
            b.Property(x => x.DebitAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.CreditAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Reference).HasMaxLength(200);
            b.HasOne(x => x.GLEntry)
             .WithMany(e => e.Lines)
             .HasForeignKey(x => x.GLEntryId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.GLAccount)
             .WithMany(a => a.GLEntryLines)
             .HasForeignKey(x => x.GLAccountId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GLBalance>(b =>
        {
            b.ToTable("GLBalances");
            b.HasKey(x => x.Id);
            b.Property(x => x.OpeningBalance).HasColumnType("decimal(18,2)");
            b.Property(x => x.DebitTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.CreditTotal).HasColumnType("decimal(18,2)");
            b.Property(x => x.ClosingBalance).HasColumnType("decimal(18,2)");
            b.HasOne(x => x.GLAccount)
             .WithMany(a => a.GLBalances)
             .HasForeignKey(x => x.GLAccountId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.GLAccountId, x.PeriodYear, x.PeriodMonth }).IsUnique();
        });

        // System-Assisted Manual Verification Configuration
        modelBuilder.Entity<DocumentVerification>(b =>
        {
            b.ToTable("DocumentVerifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
            b.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
            b.Property(x => x.DocumentImagePath).HasMaxLength(1000);
            b.Property(x => x.ManuallyEnteredData).HasColumnType("nvarchar(max)");
            b.Property(x => x.OcrExtractedData).HasColumnType("nvarchar(max)");
            b.Property(x => x.OcrConfidenceScore).HasColumnType("decimal(5,4)");
            b.Property(x => x.OcrProvider).HasMaxLength(100);
            b.Property(x => x.VerifiedBy).HasMaxLength(200);
            b.Property(x => x.VerificationNotes).HasMaxLength(2000);
            b.Property(x => x.VerificationDecisionReason).HasMaxLength(1000);
            b.Property(x => x.DataMismatches).HasColumnType("nvarchar(max)");
            b.HasOne(x => x.Client)
             .WithMany()
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ClientId);
            b.HasIndex(x => new { x.DocumentType, x.DocumentNumber });
            b.HasIndex(x => x.VerificationDate);
            b.HasIndex(x => x.IsVerified);
        });

        // User and Identity Configuration
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Username).HasMaxLength(100).IsRequired();
            b.Property(x => x.Email).HasMaxLength(255).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.Property(x => x.PhoneNumber).HasMaxLength(20);
            b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            b.Property(x => x.BranchId).HasMaxLength(50);
            b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            b.Property(x => x.UpdatedBy).HasMaxLength(100);
            b.HasIndex(x => x.Username).IsUnique();
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => x.IsActive);
            b.Property(x => x.Metadata).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());
        });

        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(x => new { x.UserId, x.RoleId });
            b.Property(x => x.AssignedBy).HasMaxLength(100).IsRequired();
            b.Property(x => x.BranchId).HasMaxLength(50);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.IsActive);
            b.Property(x => x.Metadata).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("Permissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Resource).HasMaxLength(100).IsRequired();
            b.Property(x => x.Action).HasMaxLength(50).IsRequired();
            b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            b.Property(x => x.UpdatedBy).HasMaxLength(100);
            b.HasIndex(x => x.Name).IsUnique();
            b.HasIndex(x => new { x.Resource, x.Action }).IsUnique();
            b.HasIndex(x => x.IsActive);
            b.Property(x => x.Metadata).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("RolePermissions");
            b.HasKey(x => new { x.RoleId, x.PermissionId });
            b.Property(x => x.GrantedBy).HasMaxLength(100).IsRequired();
            b.Property(x => x.Conditions).HasMaxLength(1000);
            b.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.IsActive);
            b.Property(x => x.Metadata).HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());
        });

        // Seed reference data
        var now = DateTime.UtcNow;
        modelBuilder.Entity<LoanProduct>().HasData(
            new LoanProduct { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "SALARY", Name = "Salary Advance", InterestRateAnnualPercent = 24.00m, TermMonthsDefault = 6, IsActive = true, CreatedAtUtc = now },
            new LoanProduct { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "PAYROLL", Name = "Payroll Loan", InterestRateAnnualPercent = 28.00m, TermMonthsDefault = 12, IsActive = true, CreatedAtUtc = now },
            new LoanProduct { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "SME", Name = "SME Working Capital", InterestRateAnnualPercent = 32.00m, TermMonthsDefault = 18, IsActive = true, CreatedAtUtc = now }
        );

        modelBuilder.Entity<GLAccount>().HasData(
            new GLAccount { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), AccountCode = "1000", Name = "Cash and Bank", Category = "Asset", IsActive = true },
            new GLAccount { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), AccountCode = "1100", Name = "Loans Receivable", Category = "Asset", IsActive = true },
            new GLAccount { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), AccountCode = "2000", Name = "Customer Deposits", Category = "Liability", IsActive = true },
            new GLAccount { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"), AccountCode = "3000", Name = "Share Capital", Category = "Equity", IsActive = true },
            new GLAccount { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"), AccountCode = "4000", Name = "Interest Income", Category = "Income", IsActive = true },
            new GLAccount { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"), AccountCode = "5000", Name = "Operational Expenses", Category = "Expense", IsActive = true }
        );

        // Seed default roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = "role-ceo", Name = "CEO", Description = "Chief Executive Officer", Type = RoleType.Organizational, IsSystemRole = true, Level = 1, CreatedBy = "system", CreatedAt = now },
            new Role { Id = "role-manager", Name = "Manager", Description = "Branch Manager", Type = RoleType.Organizational, IsSystemRole = true, Level = 2, CreatedBy = "system", CreatedAt = now },
            new Role { Id = "role-officer", Name = "LoanOfficer", Description = "Loan Officer", Type = RoleType.Standard, IsSystemRole = true, Level = 3, CreatedBy = "system", CreatedAt = now },
            new Role { Id = "role-analyst", Name = "Analyst", Description = "Credit Analyst", Type = RoleType.Standard, IsSystemRole = true, Level = 3, CreatedBy = "system", CreatedAt = now }
        );

        // Seed default admin user (password: Password123!)
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = "user-admin",
                Username = "admin",
                Email = "admin@intellifin.com",
                FirstName = "System",
                LastName = "Administrator",
                PasswordHash = "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PJ/...", // Password123!
                IsActive = true,
                EmailConfirmed = true,
                CreatedBy = "system",
                CreatedAt = now
            }
        );

        // Assign admin user to CEO role
        modelBuilder.Entity<UserRole>().HasData(
            new UserRole { UserId = "user-admin", RoleId = "role-ceo", AssignedBy = "system", AssignedAt = now }
        );

        base.OnModelCreating(modelBuilder);
    }
}

