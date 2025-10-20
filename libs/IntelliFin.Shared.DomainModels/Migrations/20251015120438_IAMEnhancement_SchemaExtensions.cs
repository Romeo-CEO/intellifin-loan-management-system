using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntelliFin.Shared.DomainModels.Migrations
{
    /// <inheritdoc />
    public partial class IAMEnhancement_SchemaExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_EntityType_EntityId_OccurredAtUtc",
                table: "AuditEvents");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-analyst");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-manager");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-officer");

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { "role-ceo", "user-admin" });

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-ceo");

            migrationBuilder.RenameColumn(
                name: "OccurredAtUtc",
                table: "AuditEvents",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "EntityType",
                table: "AuditEvents",
                newName: "Entity");

            migrationBuilder.RenameColumn(
                name: "Data",
                table: "AuditEvents",
                newName: "Details");

            migrationBuilder.RenameColumn(
                name: "Actor",
                table: "AuditEvents",
                newName: "ActorId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AuditEvents",
                newName: "EventId");

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchRegion",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditEvents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "AuditEvents",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "AuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "AuditEvents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ActorId",
                table: "AuditEvents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventProcessingStatus",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessingResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventProcessingStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventRoutingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConsumerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Conditions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRoutingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HealthCheckLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthCheckLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonalizationTokens = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationMs = table.Column<double>(type: "float", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAccounts",
                columns: table => new
                {
                    ServiceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAccounts", x => x.ServiceAccountId);
                });

            migrationBuilder.CreateTable(
                name: "SoDRules",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConflictingPermissions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Enforcement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "strict"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoDRules", x => x.RuleId);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "TokenRevocations",
                columns: table => new
                {
                    RevocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenRevocations", x => x.RevocationId);
                });

            migrationBuilder.CreateTable(
                name: "EventRoutingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceService = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Destinations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RouteTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RuleId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRoutingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventRoutingLogs_EventRoutingRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "EventRoutingRules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonalizationData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GatewayResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_NotificationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCredentials",
                columns: table => new
                {
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCredentials", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_ServiceCredentials_ServiceAccounts_ServiceAccountId",
                        column: x => x.ServiceAccountId,
                        principalTable: "ServiceAccounts",
                        principalColumn: "ServiceAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantBranches",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBranches", x => new { x.TenantId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_TenantBranches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => new { x.TenantId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(2700), new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(2704) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5501), new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5503) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5515), new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5516) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5521), new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5522) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5561), new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5562) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5566), new DateTime(2025, 10, 15, 14, 19, 27, 127, DateTimeKind.Utc).AddTicks(5567) });

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653));

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653));

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653));

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsSystemRole", "Level", "Name", "ParentRoleId", "Type", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "role-collections-officer", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", "Handles collections activities and payment follow-up", true, true, 3, "Collections Officer", null, 2, null, null },
                    { "role-compliance-officer", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", "Manages regulatory compliance and audit readiness", true, true, 2, "Compliance Officer", null, 2, null, null },
                    { "role-finance-manager", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", "Oversees finance operations and financial controls", true, true, 2, "Finance Manager", null, 3, null, null },
                    { "role-loan-officer", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", "Originates and manages loan applications", true, true, 3, "Loan Officer", null, 2, null, null },
                    { "role-system-admin", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", "Global administrator with full platform access", true, true, 1, "System Administrator", null, 0, null, null },
                    { "role-underwriter", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", "Performs credit underwriting and risk assessments", true, true, 3, "Underwriter", null, 2, null, null }
                });

            migrationBuilder.InsertData(
                table: "SoDRules",
                columns: new[] { "RuleId", "ConflictingPermissions", "Description", "Enforcement", "IsActive", "RuleName" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "[\"loans:create\", \"loans:approve\"]", "Prevent a single user from originating and approving the same loan", "strict", true, "sod-loan-approval" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "[\"gl:post\", \"gl:reverse\"]", "Prevent a single user from posting and reversing the same GL entries", "strict", true, "sod-gl-posting" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "[\"clients:create\", \"compliance:manage\"]", "Block when client onboarding and compliance approval are handled by the same user", "strict", true, "sod-client-approval" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "[\"payments:record\", \"payments:reverse\"]", "Warn if the same user records and reverses customer payments during reconciliation", "warning", true, "sod-payment-reconciliation" }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: "user-admin",
                columns: new[] { "BranchName", "BranchRegion", "CreatedAt" },
                values: new object[] { null, null, new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653) });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId", "AssignedAt", "AssignedBy", "BranchId", "ExpiresAt", "IsActive", "Metadata", "Reason" },
                values: new object[] { "role-system-admin", "user-admin", new DateTime(2025, 10, 15, 14, 19, 27, 124, DateTimeKind.Utc).AddTicks(8653), "system", null, null, true, "{}", null });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId",
                table: "AuditEvents",
                column: "TenantId",
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Timestamp",
                table: "AuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_EventProcessingStatus_EventId",
                table: "EventProcessingStatus",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventProcessingStatus_EventType",
                table: "EventProcessingStatus",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_EventProcessingStatus_ProcessedAt",
                table: "EventProcessingStatus",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EventProcessingStatus_ProcessingResult",
                table: "EventProcessingStatus",
                column: "ProcessingResult");

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingLogs_EventId",
                table: "EventRoutingLogs",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingLogs_EventType",
                table: "EventRoutingLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingLogs_RouteTimestamp",
                table: "EventRoutingLogs",
                column: "RouteTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingLogs_RuleId",
                table: "EventRoutingLogs",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingLogs_SourceService_RouteTimestamp",
                table: "EventRoutingLogs",
                columns: new[] { "SourceService", "RouteTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingRules_ConsumerType",
                table: "EventRoutingRules",
                column: "ConsumerType");

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingRules_CreatedAt",
                table: "EventRoutingRules",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingRules_EventType_IsActive",
                table: "EventRoutingRules",
                columns: new[] { "EventType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EventRoutingRules_Priority",
                table: "EventRoutingRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_BranchId_CreatedAt",
                table: "NotificationLogs",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_CreatedAt",
                table: "NotificationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_EventId",
                table: "NotificationLogs",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_RecipientId",
                table: "NotificationLogs",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_RecipientId_CreatedAt",
                table: "NotificationLogs",
                columns: new[] { "RecipientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Status",
                table: "NotificationLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_TemplateId",
                table: "NotificationLogs",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Category_Channel_IsActive",
                table: "NotificationTemplates",
                columns: new[] { "Category", "Channel", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_CreatedAt",
                table: "NotificationTemplates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Name_Version",
                table: "NotificationTemplates",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAccounts_ClientId",
                table: "ServiceAccounts",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCredentials_ServiceAccountId",
                table: "ServiceCredentials",
                column: "ServiceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SoDRules_RuleName",
                table: "SoDRules",
                column: "RuleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_UserId",
                table: "TenantUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenRevocations_ExpiresAt",
                table: "TokenRevocations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TokenRevocations_TokenId",
                table: "TokenRevocations",
                column: "TokenId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "EventProcessingStatus");

            migrationBuilder.DropTable(
                name: "EventRoutingLogs");

            migrationBuilder.DropTable(
                name: "HealthCheckLogs");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "PerformanceLogs");

            migrationBuilder.DropTable(
                name: "ServiceCredentials");

            migrationBuilder.DropTable(
                name: "SoDRules");

            migrationBuilder.DropTable(
                name: "TenantBranches");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.DropTable(
                name: "TokenRevocations");

            migrationBuilder.DropTable(
                name: "EventRoutingRules");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "ServiceAccounts");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_TenantId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Timestamp",
                table: "AuditEvents");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-collections-officer");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-compliance-officer");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-finance-manager");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-loan-officer");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-underwriter");

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { "role-system-admin", "user-admin" });

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "role-system-admin");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BranchRegion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditEvents");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "AuditEvents",
                newName: "OccurredAtUtc");

            migrationBuilder.RenameColumn(
                name: "Entity",
                table: "AuditEvents",
                newName: "EntityType");

            migrationBuilder.RenameColumn(
                name: "Details",
                table: "AuditEvents",
                newName: "Data");

            migrationBuilder.RenameColumn(
                name: "ActorId",
                table: "AuditEvents",
                newName: "Actor");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "AuditEvents",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "AuditEvents",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "AuditEvents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Actor",
                table: "AuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 9, 6, 10, 23, 27, 700, DateTimeKind.Utc).AddTicks(8734), new DateTime(2025, 9, 6, 10, 23, 27, 700, DateTimeKind.Utc).AddTicks(8740) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(1795), new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(1799) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2207), new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2211) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2220), new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2220) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2226), new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2226) });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"),
                columns: new[] { "CreatedAt", "LastModified" },
                values: new object[] { new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2231), new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2231) });

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436));

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436));

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436));

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsSystemRole", "Level", "Name", "ParentRoleId", "Type", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "role-analyst", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", "Credit Analyst", true, true, 3, "Analyst", null, 1, null, null },
                    { "role-ceo", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", "Chief Executive Officer", true, true, 1, "CEO", null, 3, null, null },
                    { "role-manager", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", "Branch Manager", true, true, 2, "Manager", null, 3, null, null },
                    { "role-officer", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", "Loan Officer", true, true, 3, "LoanOfficer", null, 1, null, null }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: "user-admin",
                column: "CreatedAt",
                value: new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436));

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId", "AssignedAt", "AssignedBy", "BranchId", "ExpiresAt", "IsActive", "Metadata", "Reason" },
                values: new object[] { "role-ceo", "user-admin", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", null, null, true, "{}", null });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });
        }
    }
}
