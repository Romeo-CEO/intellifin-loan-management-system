using IntelliFin.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Collections.Infrastructure.Persistence;

public class CollectionsDbContext : DbContext
{
    public CollectionsDbContext(DbContextOptions<CollectionsDbContext> options) : base(options)
    {
    }

    public DbSet<RepaymentSchedule> RepaymentSchedules => Set<RepaymentSchedule>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<ArrearsClassificationHistory> ArrearsClassificationHistory => Set<ArrearsClassificationHistory>();
    public DbSet<ReconciliationTask> ReconciliationTasks => Set<ReconciliationTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // RepaymentSchedule Configuration
        modelBuilder.Entity<RepaymentSchedule>(entity =>
        {
            entity.ToTable("RepaymentSchedules");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.ProductCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PrincipalAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.InterestRate).HasColumnType("decimal(5,4)").IsRequired();
            entity.Property(e => e.RepaymentFrequency).HasMaxLength(50).IsRequired();
            entity.Property(e => e.GeneratedBy).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(200);
            
            entity.HasIndex(e => e.LoanId).IsUnique();
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => new { e.LoanId, e.ClientId });
            entity.HasIndex(e => e.MaturityDate);
            
            entity.HasMany(e => e.Installments)
                .WithOne(i => i.RepaymentSchedule)
                .HasForeignKey(i => i.RepaymentScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Installment Configuration
        modelBuilder.Entity<Installment>(entity =>
        {
            entity.ToTable("Installments");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.PrincipalDue).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.InterestDue).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.TotalDue).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.PrincipalPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.InterestPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.TotalPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.PrincipalBalance).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DaysPastDue).HasDefaultValue(0);
            
            entity.HasIndex(e => e.RepaymentScheduleId);
            entity.HasIndex(e => new { e.RepaymentScheduleId, e.InstallmentNumber }).IsUnique();
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.DueDate });
            
            entity.HasMany(e => e.Payments)
                .WithOne(p => p.Installment)
                .HasForeignKey(p => p.InstallmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PaymentTransaction Configuration
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.TransactionReference).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PaymentMethod).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PaymentSource).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.PrincipalPortion).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.InterestPortion).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.PenaltyPortion).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsReconciled).HasDefaultValue(false);
            entity.Property(e => e.ReconciledBy).HasMaxLength(200);
            entity.Property(e => e.ExternalReference).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            
            entity.HasIndex(e => e.LoanId);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.TransactionReference).IsUnique();
            entity.HasIndex(e => e.ExternalReference);
            entity.HasIndex(e => new { e.Status, e.TransactionDate });
            entity.HasIndex(e => new { e.IsReconciled, e.TransactionDate });
            entity.HasIndex(e => e.TransactionDate);
        });

        // ArrearsClassificationHistory Configuration
        modelBuilder.Entity<ArrearsClassificationHistory>(entity =>
        {
            entity.ToTable("ArrearsClassificationHistory");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.PreviousClassification).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NewClassification).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OutstandingBalance).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.ProvisionRate).HasColumnType("decimal(5,4)").IsRequired();
            entity.Property(e => e.ProvisionAmount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.ClassifiedBy).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            
            entity.HasIndex(e => e.LoanId);
            entity.HasIndex(e => new { e.LoanId, e.ClassifiedAt });
            entity.HasIndex(e => e.NewClassification);
            entity.HasIndex(e => e.ClassifiedAt);
        });

        // ReconciliationTask Configuration
        modelBuilder.Entity<ReconciliationTask>(entity =>
        {
            entity.ToTable("ReconciliationTasks");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.TaskType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ExpectedAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ActualAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Variance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AssignedTo).HasMaxLength(200);
            entity.Property(e => e.Resolution).HasMaxLength(2000);
            entity.Property(e => e.ResolvedBy).HasMaxLength(200);
            entity.Property(e => e.CreatedBy).HasMaxLength(200).IsRequired();
            
            entity.HasIndex(e => e.PaymentTransactionId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedTo);
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        });
    }
}
