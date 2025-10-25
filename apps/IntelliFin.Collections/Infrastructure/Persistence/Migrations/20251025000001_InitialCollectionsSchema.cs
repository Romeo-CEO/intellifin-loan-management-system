using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCollectionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArrearsClassificationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NewClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DaysPastDue = table.Column<int>(type: "int", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProvisionRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ProvisionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsNonAccrual = table.Column<bool>(type: "bit", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClassifiedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrearsClassificationHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepaymentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    RepaymentFrequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaturityDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepaymentSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepaymentScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrincipalDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrincipalPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    InterestPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PrincipalBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DaysPastDue = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installments_RepaymentSchedules_RepaymentScheduleId",
                        column: x => x.RepaymentScheduleId,
                        principalTable: "RepaymentSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrincipalPortion = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    InterestPortion = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    PenaltyPortion = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciledBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "Installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationTasks_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArrearsClassificationHistory_ClassifiedAt",
                table: "ArrearsClassificationHistory",
                column: "ClassifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ArrearsClassificationHistory_LoanId",
                table: "ArrearsClassificationHistory",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_ArrearsClassificationHistory_LoanId_ClassifiedAt",
                table: "ArrearsClassificationHistory",
                columns: new[] { "LoanId", "ClassifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArrearsClassificationHistory_NewClassification",
                table: "ArrearsClassificationHistory",
                column: "NewClassification");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_DueDate",
                table: "Installments",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_RepaymentScheduleId",
                table: "Installments",
                column: "RepaymentScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_RepaymentScheduleId_InstallmentNumber",
                table: "Installments",
                columns: new[] { "RepaymentScheduleId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Installments_Status",
                table: "Installments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_Status_DueDate",
                table: "Installments",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ClientId",
                table: "PaymentTransactions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ExternalReference",
                table: "PaymentTransactions",
                column: "ExternalReference");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InstallmentId",
                table: "PaymentTransactions",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_IsReconciled_TransactionDate",
                table: "PaymentTransactions",
                columns: new[] { "IsReconciled", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_LoanId",
                table: "PaymentTransactions",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Status_TransactionDate",
                table: "PaymentTransactions",
                columns: new[] { "Status", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TransactionDate",
                table: "PaymentTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TransactionReference",
                table: "PaymentTransactions",
                column: "TransactionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationTasks_AssignedTo",
                table: "ReconciliationTasks",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationTasks_PaymentTransactionId",
                table: "ReconciliationTasks",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationTasks_Status",
                table: "ReconciliationTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationTasks_Status_CreatedAtUtc",
                table: "ReconciliationTasks",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepaymentSchedules_ClientId",
                table: "RepaymentSchedules",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_RepaymentSchedules_LoanId",
                table: "RepaymentSchedules",
                column: "LoanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepaymentSchedules_LoanId_ClientId",
                table: "RepaymentSchedules",
                columns: new[] { "LoanId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_RepaymentSchedules_MaturityDate",
                table: "RepaymentSchedules",
                column: "MaturityDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArrearsClassificationHistory");

            migrationBuilder.DropTable(
                name: "ReconciliationTasks");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "Installments");

            migrationBuilder.DropTable(
                name: "RepaymentSchedules");
        }
    }
}
