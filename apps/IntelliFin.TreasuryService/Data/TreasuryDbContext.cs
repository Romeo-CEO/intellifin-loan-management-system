using IntelliFin.TreasuryService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.TreasuryService.Data;

public class TreasuryDbContext(DbContextOptions<TreasuryDbContext> options) : DbContext(options)
{
    public DbSet<TreasuryTransaction> TreasuryTransactions => Set<TreasuryTransaction>();
    public DbSet<BranchFloat> BranchFloats => Set<BranchFloat>();
    public DbSet<BranchFloatTransaction> BranchFloatTransactions => Set<BranchFloatTransaction>();
    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();
    public DbSet<ReconciliationEntry> ReconciliationEntries => Set<ReconciliationEntry>();
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<BankStatementEntry> BankStatementEntries => Set<BankStatementEntry>();
    public DbSet<ExpenseRequest> ExpenseRequests => Set<ExpenseRequest>();
    public DbSet<ExpenseApproval> ExpenseApprovals => Set<ExpenseApproval>();
    public DbSet<EndOfDayReport> EndOfDayReports => Set<EndOfDayReport>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<LoanDisbursement> LoanDisbursements => Set<LoanDisbursement>();
    public DbSet<DisbursementApproval> DisbursementApprovals => Set<DisbursementApproval>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TreasuryTransaction configuration
        modelBuilder.Entity<TreasuryTransaction>(entity =>
        {
            entity.ToTable("TreasuryTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.TransactionId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.TransactionType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Currency)
                  .HasMaxLength(3)
                  .HasDefaultValue("MWK")
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Pending")
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ProcessedAt);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.BankReference)
                  .HasMaxLength(100);

            entity.Property(e => e.ErrorMessage)
                  .HasMaxLength(1000);

            entity.HasIndex(e => e.TransactionId)
                  .IsUnique()
                  .HasDatabaseName("IX_TreasuryTransactions_TransactionId");

            entity.HasIndex(e => e.CorrelationId)
                  .HasDatabaseName("IX_TreasuryTransactions_CorrelationId");

            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("IX_TreasuryTransactions_CreatedAt");

            entity.HasIndex(e => new { e.TransactionType, e.Status })
                  .HasDatabaseName("IX_TreasuryTransactions_TypeStatus");
        });

        // BranchFloat configuration
        modelBuilder.Entity<BranchFloat>(entity =>
        {
            entity.ToTable("BranchFloats");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.BranchId)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.BranchName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.CurrentBalance)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.LowThreshold)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.HighThreshold)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Active")
                  .IsRequired();

            entity.Property(e => e.LastUpdated)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.LastUpdatedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.BranchId)
                  .IsUnique()
                  .HasDatabaseName("IX_BranchFloats_BranchId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_BranchFloats_Status");
        });

        // BranchFloatTransaction configuration
        modelBuilder.Entity<BranchFloatTransaction>(entity =>
        {
            entity.ToTable("BranchFloatTransactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.TransactionId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.BranchId)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.TransactionType)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.BalanceAfter)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Reference)
                  .HasMaxLength(100);

            entity.Property(e => e.Description)
                  .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.BranchId)
                  .HasDatabaseName("IX_BranchFloatTransactions_BranchId");

            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("IX_BranchFloatTransactions_CreatedAt");

            entity.HasOne(e => e.BranchFloat)
                  .WithMany(bf => bf.Transactions)
                  .HasForeignKey(e => e.BranchFloatId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ReconciliationBatch configuration
        modelBuilder.Entity<ReconciliationBatch>(entity =>
        {
            entity.ToTable("ReconciliationBatches");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.BatchId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.BatchType)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.FileName)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.TotalEntries)
                  .IsRequired();

            entity.Property(e => e.ProcessedEntries);

            entity.Property(e => e.MatchedEntries);

            entity.Property(e => e.UnmatchedEntries);

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Processing")
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CompletedAt);

            entity.Property(e => e.ErrorMessage)
                  .HasMaxLength(1000);

            entity.HasIndex(e => e.BatchId)
                  .IsUnique()
                  .HasDatabaseName("IX_ReconciliationBatches_BatchId");

            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("IX_ReconciliationBatches_CreatedAt");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_ReconciliationBatches_Status");
        });

        // ReconciliationEntry configuration
        modelBuilder.Entity<ReconciliationEntry>(entity =>
        {
            entity.ToTable("ReconciliationEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.BatchId)
                  .IsRequired();

            entity.Property(e => e.EntryType)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Reference)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(500);

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.MatchStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("Unmatched")
                  .IsRequired();

            entity.Property(e => e.MatchedTransactionId);

            entity.Property(e => e.MatchConfidence)
                  .HasColumnType("decimal(5,2)");

            entity.Property(e => e.MatchMethod)
                  .HasMaxLength(50);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.BatchId)
                  .HasDatabaseName("IX_ReconciliationEntries_BatchId");

            entity.HasIndex(e => new { e.MatchStatus, e.Amount })
                  .HasDatabaseName("IX_ReconciliationEntries_MatchStatusAmount");

            entity.HasIndex(e => e.Reference)
                  .HasDatabaseName("IX_ReconciliationEntries_Reference");

            entity.HasOne(e => e.Batch)
                  .WithMany(b => b.Entries)
                  .HasForeignKey(e => e.BatchId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // BankStatement configuration
        modelBuilder.Entity<BankStatement>(entity =>
        {
            entity.ToTable("BankStatements");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.StatementId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.BankCode)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.BankName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.AccountNumber)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.StatementDate)
                  .IsRequired();

            entity.Property(e => e.OpeningBalance)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.ClosingBalance)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.TotalCredits)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.TotalDebits)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.FilePath)
                  .HasMaxLength(1000);

            entity.Property(e => e.MinioObjectKey)
                  .HasMaxLength(1000);

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Processing")
                  .IsRequired();

            entity.Property(e => e.ProcessedAt);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.StatementId)
                  .IsUnique()
                  .HasDatabaseName("IX_BankStatements_StatementId");

            entity.HasIndex(e => e.StatementDate)
                  .HasDatabaseName("IX_BankStatements_StatementDate");

            entity.HasIndex(e => e.BankCode)
                  .HasDatabaseName("IX_BankStatements_BankCode");
        });

        // BankStatementEntry configuration
        modelBuilder.Entity<BankStatementEntry>(entity =>
        {
            entity.ToTable("BankStatementEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.StatementId)
                  .IsRequired();

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.Reference)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(500);

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Balance)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.StatementId)
                  .HasDatabaseName("IX_BankStatementEntries_StatementId");

            entity.HasIndex(e => e.Reference)
                  .HasDatabaseName("IX_BankStatementEntries_Reference");

            entity.HasIndex(e => e.TransactionDate)
                  .HasDatabaseName("IX_BankStatementEntries_TransactionDate");

            entity.HasOne(e => e.BankStatement)
                  .WithMany(s => s.Entries)
                  .HasForeignKey(e => e.StatementId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ExpenseRequest configuration
        modelBuilder.Entity<ExpenseRequest>(entity =>
        {
            entity.ToTable("ExpenseRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.RequestId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.BranchId)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.RequestedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Currency)
                  .HasMaxLength(3)
                  .HasDefaultValue("MWK")
                  .IsRequired();

            entity.Property(e => e.Category)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Pending")
                  .IsRequired();

            entity.Property(e => e.Urgency)
                  .HasMaxLength(20)
                  .HasDefaultValue("Normal")
                  .IsRequired();

            entity.Property(e => e.ReceiptPath)
                  .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.RequestId)
                  .IsUnique()
                  .HasDatabaseName("IX_ExpenseRequests_RequestId");

            entity.HasIndex(e => e.BranchId)
                  .HasDatabaseName("IX_ExpenseRequests_BranchId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_ExpenseRequests_Status");

            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("IX_ExpenseRequests_CreatedAt");
        });

        // ExpenseApproval configuration
        modelBuilder.Entity<ExpenseApproval>(entity =>
        {
            entity.ToTable("ExpenseApprovals");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.RequestId)
                  .IsRequired();

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ApprovalLevel)
                  .IsRequired();

            entity.Property(e => e.Decision)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.Comments)
                  .HasMaxLength(1000);

            entity.Property(e => e.ApprovedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.RequestId)
                  .HasDatabaseName("IX_ExpenseApprovals_RequestId");

            entity.HasIndex(e => e.ApprovedBy)
                  .HasDatabaseName("IX_ExpenseApprovals_ApprovedBy");

            entity.HasOne(e => e.ExpenseRequest)
                  .WithMany(er => er.Approvals)
                  .HasForeignKey(e => e.RequestId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // EndOfDayReport configuration
        modelBuilder.Entity<EndOfDayReport>(entity =>
        {
            entity.ToTable("EndOfDayReports");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.ReportId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.ReportDate)
                  .IsRequired();

            entity.Property(e => e.BranchId)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.OpeningBalance)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.ClosingBalance)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.TotalDisbursements)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.TotalCollections)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.TotalExpenses)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("InProgress")
                  .IsRequired();

            entity.Property(e => e.GeneratedAt);

            entity.Property(e => e.GeneratedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CeoOverrideBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CeoOverrideReason)
                  .HasMaxLength(1000);

            entity.Property(e => e.CeoOverrideAt);

            entity.Property(e => e.FilePath)
                  .HasMaxLength(1000);

            entity.HasIndex(e => e.ReportId)
                  .IsUnique()
                  .HasDatabaseName("IX_EndOfDayReports_ReportId");

            entity.HasIndex(e => e.ReportDate)
                  .HasDatabaseName("IX_EndOfDayReports_ReportDate");

            entity.HasIndex(e => e.BranchId)
                  .HasDatabaseName("IX_EndOfDayReports_BranchId");
        });

        // AccountingEntry configuration
        modelBuilder.Entity<AccountingEntry>(entity =>
        {
            entity.ToTable("AccountingEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.EntryId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.AccountCode)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.AccountName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.DebitAmount)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.CreditAmount)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.TransactionDate)
                  .IsRequired();

            entity.Property(e => e.Reference)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(500);

            entity.Property(e => e.SourceTransactionId)
                  .HasMaxLength(100);

            entity.Property(e => e.BatchId)
                  .HasMaxLength(100);

            entity.Property(e => e.PostedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.PostedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.EntryId)
                  .IsUnique()
                  .HasDatabaseName("IX_AccountingEntries_EntryId");

            entity.HasIndex(e => e.AccountCode)
                  .HasDatabaseName("IX_AccountingEntries_AccountCode");

            entity.HasIndex(e => e.TransactionDate)
                  .HasDatabaseName("IX_AccountingEntries_TransactionDate");

            entity.HasIndex(e => e.SourceTransactionId)
                  .HasDatabaseName("IX_AccountingEntries_SourceTransactionId");
        });

        // LoanDisbursement configuration
        modelBuilder.Entity<LoanDisbursement>(entity =>
        {
            entity.ToTable("LoanDisbursements");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.DisbursementId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.LoanId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ClientId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Amount)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(e => e.Currency)
                  .HasMaxLength(3)
                  .HasDefaultValue("MWK")
                  .IsRequired();

            entity.Property(e => e.BankAccountNumber)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.BankCode)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.BankReference)
                  .HasMaxLength(100);

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .HasDefaultValue("Pending")
                  .IsRequired();

            entity.Property(e => e.RequestedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.RequestedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.ProcessedAt);

            entity.Property(e => e.ProcessedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.DisbursementId)
                  .IsUnique()
                  .HasDatabaseName("IX_LoanDisbursements_DisbursementId");

            entity.HasIndex(e => e.LoanId)
                  .HasDatabaseName("IX_LoanDisbursements_LoanId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_LoanDisbursements_Status");

            entity.HasIndex(e => e.RequestedAt)
                  .HasDatabaseName("IX_LoanDisbursements_RequestedAt");
        });

        // DisbursementApproval configuration
        modelBuilder.Entity<DisbursementApproval>(entity =>
        {
            entity.ToTable("DisbursementApprovals");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.DisbursementId)
                  .IsRequired();

            entity.Property(e => e.ApproverId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ApproverName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.ApprovalLevel)
                  .IsRequired();

            entity.Property(e => e.Decision)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.Comments)
                  .HasMaxLength(1000);

            entity.Property(e => e.ApprovedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.DisbursementId)
                  .HasDatabaseName("IX_DisbursementApprovals_DisbursementId");

            entity.HasIndex(e => e.ApproverId)
                  .HasDatabaseName("IX_DisbursementApprovals_ApproverId");

            entity.HasOne(e => e.LoanDisbursement)
                  .WithMany(ld => ld.Approvals)
                  .HasForeignKey(e => e.DisbursementId)
                  .HasPrincipalKey(ld => ld.DisbursementId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
