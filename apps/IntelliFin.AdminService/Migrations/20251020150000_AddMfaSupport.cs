using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MfaConfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequiresMfa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TimeoutMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaConfiguration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MfaEnrollments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enrolled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EnrolledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SecretKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MfaChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChallengeCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaConfiguration_OperationName",
                table: "MfaConfiguration",
                column: "OperationName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_ChallengeId",
                table: "MfaChallenges",
                column: "ChallengeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_ExpiresAt",
                table: "MfaChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_InitiatedAt",
                table: "MfaChallenges",
                column: "InitiatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_Status",
                table: "MfaChallenges",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_UserId",
                table: "MfaChallenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaEnrollments_UserId",
                table: "MfaEnrollments",
                column: "UserId",
                unique: true);

            migrationBuilder.InsertData(
                table: "MfaConfiguration",
                columns: new[] { "Id", "Description", "OperationName", "RequiresMfa", "TimeoutMinutes" },
                values: new object[,]
                {
                    { 1, "Loan approvals over $50,000", "LoanApproval.HighValue", true, 15 },
                    { 2, "Role assignment to users", "RoleManagement.Assign", true, 15 },
                    { 3, "Role removal from users", "RoleManagement.Remove", true, 15 },
                    { 4, "User account creation", "UserManagement.Create", true, 15 },
                    { 5, "User account deletion", "UserManagement.Delete", true, 15 },
                    { 6, "Sensitive configuration changes", "Configuration.Update", true, 15 },
                    { 7, "Customer PII data export", "DataExport.CustomerPII", true, 15 },
                    { 8, "Audit log export", "AuditLog.Export", true, 15 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaChallenges");

            migrationBuilder.DropTable(
                name: "MfaConfiguration");

            migrationBuilder.DropTable(
                name: "MfaEnrollments");
        }
    }
}
