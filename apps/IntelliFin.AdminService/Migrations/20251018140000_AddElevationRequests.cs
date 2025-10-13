using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddElevationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElevationRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElevationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedRoles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedDuration = table.Column<int>(type: "int", nullable: false),
                    ApprovedDuration = table.Column<int>(type: "int", nullable: true),
                    ManagerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ManagerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElevationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElevationRequests_ElevationId",
                table: "ElevationRequests",
                column: "ElevationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElevationRequests_ExpiresAt",
                table: "ElevationRequests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ElevationRequests_ManagerId",
                table: "ElevationRequests",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ElevationRequests_Status",
                table: "ElevationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ElevationRequests_UserId",
                table: "ElevationRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElevationRequests");
        }
    }
}
