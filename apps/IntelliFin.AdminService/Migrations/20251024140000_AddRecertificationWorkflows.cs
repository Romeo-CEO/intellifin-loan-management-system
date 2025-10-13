using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    public partial class AddRecertificationWorkflows : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecertificationCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CampaignName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quarter = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    TotalUsersInScope = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UsersReviewed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UsersApproved = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UsersRevoked = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UsersModified = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EscalationCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecertificationCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecertificationReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    GeneratedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccessedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RetentionDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecertificationReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecertificationTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ManagerUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ManagerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ManagerEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    UsersInScope = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UsersReviewed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RemindersSent = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastReminderAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CamundaTaskId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecertificationTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecertificationEscalations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EscalationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalManagerUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EscalatedToUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EscalationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResolutionComments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecertificationEscalations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecertificationReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserJobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CurrentRoles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentPermissions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccessGrantedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RiskLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RiskIndicators = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    DecisionComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecisionMadeBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecisionMadeAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RolesToRevoke = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealsSubmitted = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AppealStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecertificationReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationCampaigns_CampaignId",
                table: "RecertificationCampaigns",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationCampaigns_Status",
                table: "RecertificationCampaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationCampaigns_QuarterYear",
                table: "RecertificationCampaigns",
                columns: new[] { "Quarter", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationReports_CampaignId",
                table: "RecertificationReports",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationReports_ReportType",
                table: "RecertificationReports",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationTasks_TaskId",
                table: "RecertificationTasks",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationTasks_Manager",
                table: "RecertificationTasks",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationTasks_Status",
                table: "RecertificationTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationTasks_DueDate",
                table: "RecertificationTasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationEscalations_TaskId",
                table: "RecertificationEscalations",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationEscalations_EscalatedTo",
                table: "RecertificationEscalations",
                column: "EscalatedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationReviews_ReviewId",
                table: "RecertificationReviews",
                column: "ReviewId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationReviews_UserId",
                table: "RecertificationReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationReviews_Decision",
                table: "RecertificationReviews",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_RecertificationReviews_RiskLevel",
                table: "RecertificationReviews",
                column: "RiskLevel");

            migrationBuilder.Sql(@"CREATE VIEW vw_RecertificationCampaignSummary AS
SELECT 
    c.CampaignId,
    c.CampaignName,
    c.StartDate,
    c.DueDate,
    c.Status,
    c.TotalUsersInScope,
    c.UsersReviewed,
    c.UsersApproved,
    c.UsersRevoked,
    c.UsersModified,
    CASE WHEN c.TotalUsersInScope = 0 THEN 0 ELSE CAST(c.UsersReviewed AS FLOAT) / c.TotalUsersInScope * 100 END AS CompletionPercentage,
    COUNT(t.Id) AS ManagerTaskCount,
    SUM(CASE WHEN t.Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedTaskCount,
    SUM(CASE WHEN t.Status = 'Overdue' THEN 1 ELSE 0 END) AS OverdueTaskCount,
    c.EscalationCount
FROM RecertificationCampaigns c
LEFT JOIN RecertificationTasks t ON c.CampaignId = t.CampaignId
GROUP BY c.CampaignId, c.CampaignName, c.StartDate, c.DueDate, c.Status, c.TotalUsersInScope, c.UsersReviewed, c.UsersApproved, c.UsersRevoked, c.UsersModified, c.EscalationCount;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_RecertificationCampaignSummary;");

            migrationBuilder.DropTable(
                name: "RecertificationEscalations");

            migrationBuilder.DropTable(
                name: "RecertificationReports");

            migrationBuilder.DropTable(
                name: "RecertificationReviews");

            migrationBuilder.DropTable(
                name: "RecertificationTasks");

            migrationBuilder.DropTable(
                name: "RecertificationCampaigns");
        }
    }
}
