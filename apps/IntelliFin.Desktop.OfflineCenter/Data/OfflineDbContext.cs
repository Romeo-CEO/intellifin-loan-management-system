using IntelliFin.Desktop.OfflineCenter.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Desktop.OfflineCenter.Data;

public class OfflineDbContext : DbContext
{
    public DbSet<OfflineLoan> Loans { get; set; }
    public DbSet<OfflineClient> Clients { get; set; }
    public DbSet<OfflinePayment> Payments { get; set; }
    public DbSet<OfflineFinancialSummary> FinancialSummaries { get; set; }
    public DbSet<OfflineSyncLog> SyncLogs { get; set; }
    public DbSet<OfflineReport> Reports { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "intellifin_offline.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure OfflineLoan
        modelBuilder.Entity<OfflineLoan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LoanId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PrincipalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OutstandingBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.InterestRate).HasColumnType("decimal(5,4)");
            entity.HasIndex(e => e.LoanId).IsUnique();
        });

        // Configure OfflineClient
        modelBuilder.Entity<OfflineClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.NationalId).HasMaxLength(50);
            entity.HasIndex(e => e.ClientId).IsUnique();
        });

        // Configure OfflinePayment
        modelBuilder.Entity<OfflinePayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PaymentId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LoanId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.PaymentId).IsUnique();
        });

        // Configure OfflineFinancialSummary
        modelBuilder.Entity<OfflineFinancialSummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalLoansOutstanding).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalCollections).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalDisbursements).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalProvisions).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CashBalance).HasColumnType("decimal(18,2)");
        });

        // Configure OfflineSyncLog
        modelBuilder.Entity<OfflineSyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
        });

        // Configure OfflineReport
        modelBuilder.Entity<OfflineReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReportId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReportType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.ReportId).IsUnique();
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await Database.EnsureCreatedAsync();
    }
}
