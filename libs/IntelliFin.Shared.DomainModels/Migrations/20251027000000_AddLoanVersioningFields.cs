using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.Shared.DomainModels.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanVersioningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add versioning and audit fields to LoanApplications table
            migrationBuilder.AddColumn<string>(
                name: "LoanNumber",
                table: "LoanApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "LoanApplications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentVersionId",
                table: "LoanApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentVersion",
                table: "LoanApplications",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskGrade",
                table: "LoanApplications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EffectiveAnnualRate",
                table: "LoanApplications",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementFileHash",
                table: "LoanApplications",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementMinioPath",
                table: "LoanApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LoanApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "LoanApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAtUtc",
                table: "LoanApplications",
                type: "datetime2",
                nullable: true);

            // Create indexes for performance
            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_LoanNumber",
                table: "LoanApplications",
                column: "LoanNumber",
                unique: true,
                filter: "[LoanNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_IsCurrentVersion_Status",
                table: "LoanApplications",
                columns: new[] { "IsCurrentVersion", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_RiskGrade",
                table: "LoanApplications",
                column: "RiskGrade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_LoanApplications_LoanNumber",
                table: "LoanApplications");

            migrationBuilder.DropIndex(
                name: "IX_LoanApplications_IsCurrentVersion_Status",
                table: "LoanApplications");

            migrationBuilder.DropIndex(
                name: "IX_LoanApplications_RiskGrade",
                table: "LoanApplications");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "LoanNumber",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "ParentVersionId",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "IsCurrentVersion",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "RiskGrade",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "EffectiveAnnualRate",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "AgreementFileHash",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "AgreementMinioPath",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "LastModifiedAtUtc",
                table: "LoanApplications");
        }
    }
}
