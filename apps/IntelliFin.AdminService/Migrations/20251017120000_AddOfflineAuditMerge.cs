using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineAuditMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOfflineEvent",
                table: "AuditEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OfflineDeviceId",
                table: "AuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfflineMergeId",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfflineSessionId",
                table: "AuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalHash",
                table: "AuditEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfflineMergeHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MergeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    MergeTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OfflineSessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventsReceived = table.Column<int>(type: "int", nullable: false),
                    EventsMerged = table.Column<int>(type: "int", nullable: false),
                    DuplicatesSkipped = table.Column<int>(type: "int", nullable: false),
                    ConflictsDetected = table.Column<int>(type: "int", nullable: false),
                    EventsReHashed = table.Column<int>(type: "int", nullable: false),
                    MergeDurationMs = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineMergeHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OfflineMergeId",
                table: "AuditEvents",
                column: "OfflineMergeId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMergeHistory_MergeId",
                table: "OfflineMergeHistory",
                column: "MergeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMergeHistory_MergeTimestamp",
                table: "OfflineMergeHistory",
                column: "MergeTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineMergeHistory_UserId",
                table: "OfflineMergeHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfflineMergeHistory");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_OfflineMergeId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IsOfflineEvent",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OfflineDeviceId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OfflineMergeId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OfflineSessionId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OriginalHash",
                table: "AuditEvents");
        }
    }
}
