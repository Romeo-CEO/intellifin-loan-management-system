using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEddFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EddReportObjectKey",
                table: "KycStatuses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EddApprovedBy",
                table: "KycStatuses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EddCeoApprovedBy",
                table: "KycStatuses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EddApprovedAt",
                table: "KycStatuses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskAcceptanceLevel",
                table: "KycStatuses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplianceComments",
                table: "KycStatuses",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CeoComments",
                table: "KycStatuses",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            // Add check constraint for RiskAcceptanceLevel
            migrationBuilder.Sql(@"
                ALTER TABLE KycStatuses 
                ADD CONSTRAINT CK_KycStatuses_RiskAcceptanceLevel 
                CHECK (RiskAcceptanceLevel IS NULL OR RiskAcceptanceLevel IN ('Standard', 'EnhancedMonitoring', 'RestrictedServices'))
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EddReportObjectKey",
                table: "KycStatuses");

            migrationBuilder.DropColumn(
                name: "EddApprovedBy",
                table: "KycStatuses");

            migrationBuilder.DropColumn(
                name: "EddCeoApprovedBy",
                table: "KycStatuses");

            migrationBuilder.DropColumn(
                name: "EddApprovedAt",
                table: "KycStatuses");

            migrationBuilder.DropColumn(
                name: "RiskAcceptanceLevel",
                table: "KycStatuses");

            migrationBuilder.DropColumn(
                name: "ComplianceComments",
                table: "KycStatuses");

            migrationBuilder.DropColumn(
                name: "CeoComments",
                table: "KycStatuses");
        }
    }
}
