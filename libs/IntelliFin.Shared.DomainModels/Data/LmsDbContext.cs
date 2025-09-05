using IntelliFin.Shared.DomainModels.Entities;
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
            b.Property(x => x.TermMonths).IsRequired();
            b.Property(x => x.ProductCode).HasMaxLength(64).IsRequired();
            b.Property(x => x.Status).HasMaxLength(32).IsRequired();
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.HasOne(x => x.Client)
             .WithMany(c => c.LoanApplications)
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ClientId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<LoanProduct>(b =>
        {
            b.ToTable("LoanProducts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.InterestRateAnnualPercent).HasColumnType("decimal(5,2)").IsRequired();
            b.Property(x => x.TermMonthsDefault).IsRequired();
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<GLAccount>(b =>
        {
            b.ToTable("GLAccounts");
            b.HasKey(x => x.Id);
            b.Property(x => x.AccountCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Category).HasMaxLength(50).IsRequired();
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.HasIndex(x => x.AccountCode).IsUnique();
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

        base.OnModelCreating(modelBuilder);
    }
}

