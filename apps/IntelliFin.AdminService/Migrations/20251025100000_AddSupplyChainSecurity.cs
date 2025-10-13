using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    public partial class AddSupplyChainSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContainerImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImageDigest = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Registry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BuildNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GitCommitSha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BuildTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSigned = table.Column<bool>(type: "bit", nullable: false),
                    SignatureVerified = table.Column<bool>(type: "bit", nullable: false),
                    SignatureTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HasSbom = table.Column<bool>(type: "bit", nullable: false),
                    SbomPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SbomFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VulnerabilityScanCompleted = table.Column<bool>(type: "bit", nullable: false),
                    VulnerabilityScanTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriticalCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    HighCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MediumCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LowCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DeployedToProduction = table.Column<bool>(type: "bit", nullable: false),
                    DeploymentTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignatureVerificationAudit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageDigest = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VerificationTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    VerificationResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VerificationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VerifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerificationContext = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureVerificationAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vulnerabilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContainerImageId = table.Column<long>(type: "bigint", nullable: false),
                    VulnerabilityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstalledVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FixedVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Cvss3Score = table.Column<decimal>(type: "decimal(3,1)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgmentComments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MitigationPlan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetFixDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vulnerabilities_ContainerImages_ContainerImageId",
                        column: x => x.ContainerImageId,
                        principalTable: "ContainerImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerImages_BuildTimestamp",
                table: "ContainerImages",
                column: "BuildTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerImages_IsSigned",
                table: "ContainerImages",
                column: "IsSigned");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerImages_ScanCompleted",
                table: "ContainerImages",
                column: "VulnerabilityScanCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerImages_ServiceVersion",
                table: "ContainerImages",
                columns: new[] { "ServiceName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatureAudit_ImageDigest",
                table: "SignatureVerificationAudit",
                column: "ImageDigest");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureAudit_Result",
                table: "SignatureVerificationAudit",
                column: "VerificationResult");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureAudit_Timestamp",
                table: "SignatureVerificationAudit",
                column: "VerificationTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_Severity",
                table: "Vulnerabilities",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_Status",
                table: "Vulnerabilities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_VulnerabilityId",
                table: "Vulnerabilities",
                column: "VulnerabilityId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatureVerificationAudit");

            migrationBuilder.DropTable(
                name: "Vulnerabilities");

            migrationBuilder.DropTable(
                name: "ContainerImages");
        }
    }
}
