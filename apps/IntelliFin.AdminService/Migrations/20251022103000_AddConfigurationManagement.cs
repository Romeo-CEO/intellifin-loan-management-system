using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalWorkflow = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Sensitivity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AllowedValuesRegex = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AllowedValuesList = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KubernetesNamespace = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KubernetesConfigMap = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConfigMapKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    ConfigKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sensitivity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GitCommitSha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GitRepository = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GitBranch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KubernetesNamespace = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KubernetesConfigMap = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConfigMapKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationRollbacks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RollbackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    OriginalChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RolledBackValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RolledBackBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RolledBackAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationRollbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationPolicies_Category",
                table: "ConfigurationPolicies",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationPolicies_ConfigKey",
                table: "ConfigurationPolicies",
                column: "ConfigKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationPolicies_Sensitivity",
                table: "ConfigurationPolicies",
                column: "Sensitivity");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChanges_ChangeRequestId",
                table: "ConfigurationChanges",
                column: "ChangeRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChanges_ConfigKey",
                table: "ConfigurationChanges",
                column: "ConfigKey");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChanges_RequestedAt",
                table: "ConfigurationChanges",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationChanges_Status",
                table: "ConfigurationChanges",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationRollbacks_NewId",
                table: "ConfigurationRollbacks",
                column: "NewChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationRollbacks_OriginalId",
                table: "ConfigurationRollbacks",
                column: "OriginalChangeRequestId");

            migrationBuilder.InsertData(
                table: "ConfigurationPolicies",
                columns: new[] { "Id", "AllowedValuesList", "AllowedValuesRegex", "ApprovalWorkflow", "Category", "ConfigKey", "ConfigMapKey", "Description", "KubernetesConfigMap", "KubernetesNamespace", "RequiresApproval", "Sensitivity", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, null, null, "Security", "jwt.expiry.minutes", "JwtExpiryMinutes", "JWT token expiration time in minutes", "identity-service-config", "default", true, "High", null },
                    { 2, null, null, null, "Security", "jwt.refresh.expiry.days", "RefreshExpiryDays", "Refresh token expiration time in days", "identity-service-config", "default", true, "High", null },
                    { 3, null, null, null, "Application", "loan.approval.threshold", "ApprovalThreshold", "Loan amount requiring senior approval", "loan-service-config", "default", true, "Critical", null },
                    { 4, null, null, null, "Security", "audit.retention.days", "AuditRetentionDays", "Audit log retention period in days", "admin-service-config", "default", true, "High", null },
                    { 5, null, null, null, "Infrastructure", "api.rate.limit.requests", "RateLimitRequests", "API rate limit requests per minute", "api-gateway-config", "default", false, "Medium", null },
                    { 6, "[\"Debug\",\"Info\",\"Warning\",\"Error\"]", null, null, "Application", "logging.level", "LogLevel", "Application logging level (Debug, Info, Warning, Error)", "api-gateway-config", "default", false, "Low", null },
                    { 7, null, null, null, "Security", "mfa.required.threshold", "MfaThreshold", "Transaction amount requiring MFA", "identity-service-config", "default", true, "Critical", null },
                    { 8, null, null, null, "Infrastructure", "database.connection.timeout", "DbConnectionTimeout", "Database connection timeout in seconds", "loan-service-config", "default", false, "Medium", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationRollbacks");

            migrationBuilder.DropTable(
                name: "ConfigurationChanges");

            migrationBuilder.DropTable(
                name: "ConfigurationPolicies");
        }
    }
}
