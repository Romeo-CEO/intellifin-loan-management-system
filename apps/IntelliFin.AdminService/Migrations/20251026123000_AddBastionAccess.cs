using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    public partial class AddBastionAccess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BastionAccessRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetHosts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessDurationHours = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeniedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeniedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DenialReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SshCertificateIssued = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VaultCertificatePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CertificateSerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertificateContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificateExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BastionAccessRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyAccessLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmergencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovedBy1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovedBy2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IncidentTicketId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VaultOneTimeToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TokenUsed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TokenUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostIncidentReviewCompleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReviewCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BastionSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AccessRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BastionHost = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetHost = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    RecordingPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordingSize = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TerminationReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CommandCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BastionSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BastionSessions_BastionAccessRequests_AccessRequestId",
                        column: x => x.AccessRequestId,
                        principalTable: "BastionAccessRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BastionAccessRequests_RequestId",
                table: "BastionAccessRequests",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BastionAccessRequests_Status",
                table: "BastionAccessRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BastionAccessRequests_UserId",
                table: "BastionAccessRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BastionSessions_AccessRequestId",
                table: "BastionSessions",
                column: "AccessRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BastionSessions_SessionId",
                table: "BastionSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BastionSessions_Status",
                table: "BastionSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BastionSessions_Username",
                table: "BastionSessions",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAccessLogs_EmergencyId",
                table: "EmergencyAccessLogs",
                column: "EmergencyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAccessLogs_IncidentTicketId",
                table: "EmergencyAccessLogs",
                column: "IncidentTicketId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BastionSessions");

            migrationBuilder.DropTable(
                name: "EmergencyAccessLogs");

            migrationBuilder.DropTable(
                name: "BastionAccessRequests");
        }
    }
}
