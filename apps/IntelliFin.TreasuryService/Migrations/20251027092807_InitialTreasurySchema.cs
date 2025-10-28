using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.TreasuryService.Migrations
{
    /// <inheritdoc />
    public partial class InitialTreasurySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AccountCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    PostedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankStatements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MinioObjectKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Processing"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BranchFloats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LowThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HighThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchFloats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EndOfDayReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDisbursements = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCollections = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeneratedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CeoOverrideBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CeoOverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CeoOverrideAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndOfDayReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BranchId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "MWK"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Urgency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    ReceiptPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoanDisbursements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisbursementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    LoanId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "MWK"),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanDisbursements", x => x.Id);
                    table.UniqueConstraint("AK_LoanDisbursements_DisbursementId", x => x.DisbursementId);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BatchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TotalEntries = table.Column<int>(type: "int", nullable: false),
                    ProcessedEntries = table.Column<int>(type: "int", nullable: true),
                    MatchedEntries = table.Column<int>(type: "int", nullable: true),
                    UnmatchedEntries = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Processing"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TreasuryTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    TransactionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "MWK"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatementId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementEntries_BankStatements_StatementId",
                        column: x => x.StatementId,
                        principalTable: "BankStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchFloatTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    BranchFloatId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchFloatTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchFloatTransactions_BranchFloats_BranchFloatId",
                        column: x => x.BranchFloatId,
                        principalTable: "BranchFloats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseApprovals_ExpenseRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ExpenseRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MatchStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Unmatched"),
                    MatchedTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchConfidence = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MatchMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationEntries_ReconciliationBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ReconciliationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisbursementApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisbursementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TreasuryTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementApprovals_LoanDisbursements_DisbursementId",
                        column: x => x.DisbursementId,
                        principalTable: "LoanDisbursements",
                        principalColumn: "DisbursementId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisbursementApprovals_TreasuryTransactions_TreasuryTransactionId",
                        column: x => x.TreasuryTransactionId,
                        principalTable: "TreasuryTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_AccountCode",
                table: "AccountingEntries",
                column: "AccountCode");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_EntryId",
                table: "AccountingEntries",
                column: "EntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_SourceTransactionId",
                table: "AccountingEntries",
                column: "SourceTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_TransactionDate",
                table: "AccountingEntries",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_Reference",
                table: "BankStatementEntries",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_StatementId",
                table: "BankStatementEntries",
                column: "StatementId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementEntries_TransactionDate",
                table: "BankStatementEntries",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_BankCode",
                table: "BankStatements",
                column: "BankCode");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_StatementDate",
                table: "BankStatements",
                column: "StatementDate");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_StatementId",
                table: "BankStatements",
                column: "StatementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchFloats_BranchId",
                table: "BranchFloats",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchFloats_Status",
                table: "BranchFloats",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BranchFloatTransactions_BranchFloatId",
                table: "BranchFloatTransactions",
                column: "BranchFloatId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchFloatTransactions_BranchId",
                table: "BranchFloatTransactions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchFloatTransactions_CreatedAt",
                table: "BranchFloatTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementApprovals_ApproverId",
                table: "DisbursementApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementApprovals_DisbursementId",
                table: "DisbursementApprovals",
                column: "DisbursementId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementApprovals_TreasuryTransactionId",
                table: "DisbursementApprovals",
                column: "TreasuryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EndOfDayReports_BranchId",
                table: "EndOfDayReports",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_EndOfDayReports_ReportDate",
                table: "EndOfDayReports",
                column: "ReportDate");

            migrationBuilder.CreateIndex(
                name: "IX_EndOfDayReports_ReportId",
                table: "EndOfDayReports",
                column: "ReportId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseApprovals_ApprovedBy",
                table: "ExpenseApprovals",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseApprovals_RequestId",
                table: "ExpenseApprovals",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseRequests_BranchId",
                table: "ExpenseRequests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseRequests_CreatedAt",
                table: "ExpenseRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseRequests_RequestId",
                table: "ExpenseRequests",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseRequests_Status",
                table: "ExpenseRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LoanDisbursements_DisbursementId",
                table: "LoanDisbursements",
                column: "DisbursementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanDisbursements_LoanId",
                table: "LoanDisbursements",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanDisbursements_RequestedAt",
                table: "LoanDisbursements",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoanDisbursements_Status",
                table: "LoanDisbursements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_BatchId",
                table: "ReconciliationBatches",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_CreatedAt",
                table: "ReconciliationBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_Status",
                table: "ReconciliationBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationEntries_BatchId",
                table: "ReconciliationEntries",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationEntries_MatchStatusAmount",
                table: "ReconciliationEntries",
                columns: new[] { "MatchStatus", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationEntries_Reference",
                table: "ReconciliationEntries",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_CorrelationId",
                table: "TreasuryTransactions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_CreatedAt",
                table: "TreasuryTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_TransactionId",
                table: "TreasuryTransactions",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryTransactions_TypeStatus",
                table: "TreasuryTransactions",
                columns: new[] { "TransactionType", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingEntries");

            migrationBuilder.DropTable(
                name: "BankStatementEntries");

            migrationBuilder.DropTable(
                name: "BranchFloatTransactions");

            migrationBuilder.DropTable(
                name: "DisbursementApprovals");

            migrationBuilder.DropTable(
                name: "EndOfDayReports");

            migrationBuilder.DropTable(
                name: "ExpenseApprovals");

            migrationBuilder.DropTable(
                name: "ReconciliationEntries");

            migrationBuilder.DropTable(
                name: "BankStatements");

            migrationBuilder.DropTable(
                name: "BranchFloats");

            migrationBuilder.DropTable(
                name: "LoanDisbursements");

            migrationBuilder.DropTable(
                name: "TreasuryTransactions");

            migrationBuilder.DropTable(
                name: "ExpenseRequests");

            migrationBuilder.DropTable(
                name: "ReconciliationBatches");
        }
    }
}
