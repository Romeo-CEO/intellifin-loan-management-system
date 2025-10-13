using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditArchiveMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditArchiveMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArchiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventDateStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventDateEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventCount = table.Column<int>(type: "int", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CompressionRatio = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    ChainStartHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ChainEndHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreviousDayEndHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RetentionExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "PRIMARY"),
                    ReplicationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    LastReplicationCheckUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditArchiveMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditArchiveMetadata_ArchiveId",
                table: "AuditArchiveMetadata",
                column: "ArchiveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditArchiveMetadata_EventDateRange",
                table: "AuditArchiveMetadata",
                columns: new[] { "EventDateStart", "EventDateEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditArchiveMetadata_ExportDate",
                table: "AuditArchiveMetadata",
                column: "ExportDate");

            migrationBuilder.CreateIndex(
                name: "IX_AuditArchiveMetadata_ObjectKey",
                table: "AuditArchiveMetadata",
                column: "ObjectKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditArchiveMetadata");
        }
    }
}
