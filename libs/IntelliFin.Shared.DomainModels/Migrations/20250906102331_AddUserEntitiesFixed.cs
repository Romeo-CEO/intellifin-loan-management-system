using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntelliFin.Shared.DomainModels.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEntitiesFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseInterestRate",
                table: "LoanProducts",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "LoanProducts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "LoanProducts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAmount",
                table: "LoanProducts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaxTermMonths",
                table: "LoanProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MinAmount",
                table: "LoanProducts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MinTermMonths",
                table: "LoanProducts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                table: "LoanApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationDataJson",
                table: "LoanApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "LoanApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "LoanApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "LoanApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "LoanApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedAmount",
                table: "LoanApplications",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "LoanApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowInstanceId",
                table: "LoanApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "GLAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "GLAccounts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "GLAccounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsContraAccount",
                table: "GLAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "GLAccounts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "GLAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentAccountId",
                table: "GLAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LoanProducts_Code",
                table: "LoanProducts",
                column: "Code");

            migrationBuilder.CreateTable(
                name: "ApplicationFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ValidationPattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationFields_LoanProducts_LoanProductId",
                        column: x => x.LoanProductId,
                        principalTable: "LoanProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskGrade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreditScore = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    DebtToIncomeRatio = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PaymentCapacity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HasCreditBureauData = table.Column<bool>(type: "bit", nullable: false),
                    ScoreExplanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssessedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditAssessments_LoanApplications_LoanApplicationId",
                        column: x => x.LoanApplicationId,
                        principalTable: "LoanApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentImagePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ManuallyEnteredData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OcrExtractedData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OcrConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    OcrProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VerificationDecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DataMismatches = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasDataMismatches = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVerifications_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GLBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DebitTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GLBalances_GLAccounts_GLAccountId",
                        column: x => x.GLAccountId,
                        principalTable: "GLAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GLEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BatchId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemPermission = table.Column<bool>(type: "bit", nullable: false),
                    ParentPermissionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_Permissions_ParentPermissionId",
                        column: x => x.ParentPermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    ParentRoleId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Roles_ParentRoleId",
                        column: x => x.ParentRoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValidationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Expression = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationRules_LoanProducts_LoanProductId",
                        column: x => x.LoanProductId,
                        principalTable: "LoanProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    Impact = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditFactors_CreditAssessments_CreditAssessmentId",
                        column: x => x.CreditAssessmentId,
                        principalTable: "CreditAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskIndicators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Impact = table.Column<decimal>(type: "decimal(8,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskIndicators_CreditAssessments_CreditAssessmentId",
                        column: x => x.CreditAssessmentId,
                        principalTable: "CreditAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GLEntryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GLAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GLEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GLEntryLines_GLAccounts_GLAccountId",
                        column: x => x.GLAccountId,
                        principalTable: "GLAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GLEntryLines_GLEntries_GLEntryId",
                        column: x => x.GLEntryId,
                        principalTable: "GLEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PermissionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Conditions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                columns: new[] { "AccountType", "CreatedAt", "CurrentBalance", "IsContraAccount", "LastModified", "Level", "ParentAccountId" },
                values: new object[] { "", new DateTime(2025, 9, 6, 10, 23, 27, 700, DateTimeKind.Utc).AddTicks(8734), 0m, false, new DateTime(2025, 9, 6, 10, 23, 27, 700, DateTimeKind.Utc).AddTicks(8740), 0, null });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
                columns: new[] { "AccountType", "CreatedAt", "CurrentBalance", "IsContraAccount", "LastModified", "Level", "ParentAccountId" },
                values: new object[] { "", new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(1795), 0m, false, new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(1799), 0, null });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
                columns: new[] { "AccountType", "CreatedAt", "CurrentBalance", "IsContraAccount", "LastModified", "Level", "ParentAccountId" },
                values: new object[] { "", new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2207), 0m, false, new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2211), 0, null });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"),
                columns: new[] { "AccountType", "CreatedAt", "CurrentBalance", "IsContraAccount", "LastModified", "Level", "ParentAccountId" },
                values: new object[] { "", new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2220), 0m, false, new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2220), 0, null });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
                columns: new[] { "AccountType", "CreatedAt", "CurrentBalance", "IsContraAccount", "LastModified", "Level", "ParentAccountId" },
                values: new object[] { "", new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2226), 0m, false, new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2226), 0, null });

            migrationBuilder.UpdateData(
                table: "GLAccounts",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"),
                columns: new[] { "AccountType", "CreatedAt", "CurrentBalance", "IsContraAccount", "LastModified", "Level", "ParentAccountId" },
                values: new object[] { "", new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2231), 0m, false, new DateTime(2025, 9, 6, 10, 23, 27, 701, DateTimeKind.Utc).AddTicks(2231), 0, null });

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "BaseInterestRate", "Category", "CreatedAtUtc", "Description", "MaxAmount", "MaxTermMonths", "MinAmount", "MinTermMonths" },
                values: new object[] { 0m, "", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "", 0m, 0, 0m, 0 });

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "BaseInterestRate", "Category", "CreatedAtUtc", "Description", "MaxAmount", "MaxTermMonths", "MinAmount", "MinTermMonths" },
                values: new object[] { 0m, "", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "", 0m, 0, 0m, 0 });

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "BaseInterestRate", "Category", "CreatedAtUtc", "Description", "MaxAmount", "MaxTermMonths", "MinAmount", "MinTermMonths" },
                values: new object[] { 0m, "", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "", 0m, 0, 0m, 0 });

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

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AccessFailedCount", "BranchId", "CreatedAt", "CreatedBy", "Email", "EmailConfirmed", "FirstName", "IsActive", "LastLoginAt", "LastName", "LockoutEnabled", "LockoutEnd", "Metadata", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "UpdatedAt", "UpdatedBy", "Username" },
                values: new object[] { "user-admin", 0, null, new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", "admin@intellifin.com", true, "System", true, null, "Administrator", true, null, "{}", "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PJ/...", null, false, false, null, null, "admin" });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId", "AssignedAt", "AssignedBy", "BranchId", "ExpiresAt", "IsActive", "Metadata", "Reason" },
                values: new object[] { "role-ceo", "user-admin", new DateTime(2025, 9, 6, 10, 23, 27, 694, DateTimeKind.Utc).AddTicks(8436), "system", null, null, true, "{}", null });

            migrationBuilder.CreateIndex(
                name: "IX_LoanProducts_Category",
                table: "LoanProducts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_ProductCode",
                table: "LoanApplications",
                column: "ProductCode");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_Status",
                table: "LoanApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccounts_Category",
                table: "GLAccounts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_GLAccounts_ParentAccountId",
                table: "GLAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFields_LoanProductId",
                table: "ApplicationFields",
                column: "LoanProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditAssessments_AssessedAt",
                table: "CreditAssessments",
                column: "AssessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CreditAssessments_LoanApplicationId",
                table: "CreditAssessments",
                column: "LoanApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditAssessments_RiskGrade",
                table: "CreditAssessments",
                column: "RiskGrade");

            migrationBuilder.CreateIndex(
                name: "IX_CreditFactors_CreditAssessmentId",
                table: "CreditFactors",
                column: "CreditAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVerifications_ClientId",
                table: "DocumentVerifications",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVerifications_DocumentType_DocumentNumber",
                table: "DocumentVerifications",
                columns: new[] { "DocumentType", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVerifications_IsVerified",
                table: "DocumentVerifications",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVerifications_VerificationDate",
                table: "DocumentVerifications",
                column: "VerificationDate");

            migrationBuilder.CreateIndex(
                name: "IX_GLBalances_GLAccountId_PeriodYear_PeriodMonth",
                table: "GLBalances",
                columns: new[] { "GLAccountId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GLEntries_BatchId",
                table: "GLEntries",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GLEntries_EntryNumber",
                table: "GLEntries",
                column: "EntryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GLEntries_TransactionDate",
                table: "GLEntries",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_GLEntryLines_GLAccountId",
                table: "GLEntryLines",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GLEntryLines_GLEntryId",
                table: "GLEntryLines",
                column: "GLEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_IsActive",
                table: "Permissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ParentPermissionId",
                table: "Permissions",
                column: "ParentPermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Resource_Action",
                table: "Permissions",
                columns: new[] { "Resource", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskIndicators_CreditAssessmentId",
                table: "RiskIndicators",
                column: "CreditAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_IsActive",
                table: "RolePermissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_ParentRoleId",
                table: "Roles",
                column: "ParentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_IsActive",
                table: "UserRoles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationRules_LoanProductId",
                table: "ValidationRules",
                column: "LoanProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_GLAccounts_GLAccounts_ParentAccountId",
                table: "GLAccounts",
                column: "ParentAccountId",
                principalTable: "GLAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanApplications_LoanProducts_ProductCode",
                table: "LoanApplications",
                column: "ProductCode",
                principalTable: "LoanProducts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GLAccounts_GLAccounts_ParentAccountId",
                table: "GLAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_LoanApplications_LoanProducts_ProductCode",
                table: "LoanApplications");

            migrationBuilder.DropTable(
                name: "ApplicationFields");

            migrationBuilder.DropTable(
                name: "CreditFactors");

            migrationBuilder.DropTable(
                name: "DocumentVerifications");

            migrationBuilder.DropTable(
                name: "GLBalances");

            migrationBuilder.DropTable(
                name: "GLEntryLines");

            migrationBuilder.DropTable(
                name: "RiskIndicators");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "ValidationRules");

            migrationBuilder.DropTable(
                name: "GLEntries");

            migrationBuilder.DropTable(
                name: "CreditAssessments");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LoanProducts_Code",
                table: "LoanProducts");

            migrationBuilder.DropIndex(
                name: "IX_LoanProducts_Category",
                table: "LoanProducts");

            migrationBuilder.DropIndex(
                name: "IX_LoanApplications_ProductCode",
                table: "LoanApplications");

            migrationBuilder.DropIndex(
                name: "IX_LoanApplications_Status",
                table: "LoanApplications");

            migrationBuilder.DropIndex(
                name: "IX_GLAccounts_Category",
                table: "GLAccounts");

            migrationBuilder.DropIndex(
                name: "IX_GLAccounts_ParentAccountId",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "BaseInterestRate",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "MaxAmount",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "MaxTermMonths",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "MinAmount",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "MinTermMonths",
                table: "LoanProducts");

            migrationBuilder.DropColumn(
                name: "ApplicationDataJson",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "RequestedAmount",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "IsContraAccount",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "GLAccounts");

            migrationBuilder.DropColumn(
                name: "ParentAccountId",
                table: "GLAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                table: "LoanApplications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 9, 5, 15, 19, 41, 990, DateTimeKind.Utc).AddTicks(1704));

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 9, 5, 15, 19, 41, 990, DateTimeKind.Utc).AddTicks(1704));

            migrationBuilder.UpdateData(
                table: "LoanProducts",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAtUtc",
                value: new DateTime(2025, 9, 5, 15, 19, 41, 990, DateTimeKind.Utc).AddTicks(1704));
        }
    }
}
