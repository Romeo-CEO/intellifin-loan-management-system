using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentPlaybooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlaybookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AlertName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "critical"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DiagnosisSteps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolutionSteps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EscalationPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedRunbookUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AutomationProcessKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentPlaybooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AlertName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "critical"),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Open"),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PagerDutyIncidentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SlackThreadUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IncidentPlaybookId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    PostmortemDueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostmortemCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostmortemSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AutomationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalIncidents_IncidentPlaybooks_IncidentPlaybookId",
                        column: x => x.IncidentPlaybookId,
                        principalTable: "IncidentPlaybooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AlertSilenceAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    SilenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Matchers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AlertmanagerUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertSilenceAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncidentPlaybookRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    IncidentPlaybookId = table.Column<int>(type: "int", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlertName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionMinutes = table.Column<double>(type: "float", nullable: true),
                    AutomationInvoked = table.Column<bool>(type: "bit", nullable: false),
                    AutomationOutcome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PagerDutyIncidentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentPlaybookRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentPlaybookRuns_IncidentPlaybooks_IncidentPlaybookId",
                        column: x => x.IncidentPlaybookId,
                        principalTable: "IncidentPlaybooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertSilenceAudits_SilenceId",
                table: "AlertSilenceAudits",
                column: "SilenceId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentPlaybookRuns_IncidentId",
                table: "IncidentPlaybookRuns",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentPlaybookRuns_IncidentPlaybookId",
                table: "IncidentPlaybookRuns",
                column: "IncidentPlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentPlaybookRuns_RunId",
                table: "IncidentPlaybookRuns",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentPlaybooks_AlertName",
                table: "IncidentPlaybooks",
                column: "AlertName");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentPlaybooks_IsActive",
                table: "IncidentPlaybooks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentPlaybooks_PlaybookId",
                table: "IncidentPlaybooks",
                column: "PlaybookId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIncidents_AlertName",
                table: "OperationalIncidents",
                column: "AlertName");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIncidents_IncidentId",
                table: "OperationalIncidents",
                column: "IncidentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIncidents_IncidentPlaybookId",
                table: "OperationalIncidents",
                column: "IncidentPlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalIncidents_Status",
                table: "OperationalIncidents",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertSilenceAudits");

            migrationBuilder.DropTable(
                name: "IncidentPlaybookRuns");

            migrationBuilder.DropTable(
                name: "OperationalIncidents");

            migrationBuilder.DropTable(
                name: "IncidentPlaybooks");
        }
    }
}
