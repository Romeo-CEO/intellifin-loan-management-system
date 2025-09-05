using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntelliFin.Shared.DomainModels.Migrations
{
    /// <inheritdoc />
    public partial class SeedReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InterestRateAnnualPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TermMonthsDefault = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanProducts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GLAccounts",
                columns: new[] { "Id", "AccountCode", "Category", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "1000", "Asset", true, "Cash and Bank" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "1100", "Asset", true, "Loans Receivable" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), "2000", "Liability", true, "Customer Deposits" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"), "3000", "Equity", true, "Share Capital" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"), "4000", "Income", true, "Interest Income" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"), "5000", "Expense", true, "Operational Expenses" }
                });

            migrationBuilder.InsertData(
                table: "LoanProducts",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "InterestRateAnnualPercent", "IsActive", "Name", "TermMonthsDefault" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "SALARY", new DateTime(2025, 9, 5, 15, 19, 41, 990, DateTimeKind.Utc).AddTicks(1704), 24.00m, true, "Salary Advance", 6 },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "PAYROLL", new DateTime(2025, 9, 5, 15, 19, 41, 990, DateTimeKind.Utc).AddTicks(1704), 28.00m, true, "Payroll Loan", 12 },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "SME", new DateTime(2025, 9, 5, 15, 19, 41, 990, DateTimeKind.Utc).AddTicks(1704), 32.00m, true, "SME Working Capital", 18 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanProducts_Code",
                table: "LoanProducts",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanProducts");

            migrationBuilder.DeleteData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"));

            migrationBuilder.DeleteData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"));

            migrationBuilder.DeleteData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"));

            migrationBuilder.DeleteData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"));

            migrationBuilder.DeleteData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"));

            migrationBuilder.DeleteData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"));
        }
    }
}
