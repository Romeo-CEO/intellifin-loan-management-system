using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
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
                    KycStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KycCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KycCompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmlRiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPep = table.Column<bool>(type: "bit", nullable: false),
                    IsSanctioned = table.Column<bool>(type: "bit", nullable: false),
                    RiskRating = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RiskLastAssessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ChangeSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientVersions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Composite unique index - No duplicate version numbers for same client
            migrationBuilder.CreateIndex(
                name: "IX_ClientVersions_ClientId_VersionNumber",
                table: "ClientVersions",
                columns: new[] { "ClientId", "VersionNumber" },
                unique: true);

            // Temporal query index - Optimizes point-in-time queries
            migrationBuilder.CreateIndex(
                name: "IX_ClientVersions_ClientId_ValidFrom_ValidTo",
                table: "ClientVersions",
                columns: new[] { "ClientId", "ValidFrom", "ValidTo" });

            // Current version index - Fast lookup of active version
            migrationBuilder.CreateIndex(
                name: "IX_ClientVersions_ClientId_IsCurrent",
                table: "ClientVersions",
                columns: new[] { "ClientId", "IsCurrent" });

            // Unique filtered index - Ensures only one IsCurrent=true per ClientId
            migrationBuilder.CreateIndex(
                name: "IX_ClientVersions_ClientId_IsCurrent_Unique",
                table: "ClientVersions",
                column: "ClientId",
                unique: true,
                filter: "[IsCurrent] = 1");

            // Additional indexes for performance
            migrationBuilder.CreateIndex(
                name: "IX_ClientVersions_ValidFrom",
                table: "ClientVersions",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_ClientVersions_CreatedAt",
                table: "ClientVersions",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientVersions");
        }
    }
}
