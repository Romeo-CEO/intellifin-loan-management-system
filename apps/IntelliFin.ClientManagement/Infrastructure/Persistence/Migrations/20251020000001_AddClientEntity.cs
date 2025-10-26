using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nrc = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    PayrollNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OtherNames = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaritalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Ministry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmployerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EmploymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PrimaryPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SecondaryPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KycStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    KycCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KycCompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmlRiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Low"),
                    IsPep = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSanctioned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RiskRating = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Low"),
                    RiskLastAssessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.CheckConstraint("CK_Clients_VersionNumber", "[VersionNumber] >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_BranchId",
                table: "Clients",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedAt",
                table: "Clients",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_KycStatus",
                table: "Clients",
                column: "KycStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Nrc",
                table: "Clients",
                column: "Nrc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PayrollNumber",
                table: "Clients",
                column: "PayrollNumber",
                unique: true,
                filter: "[PayrollNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Status",
                table: "Clients",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
