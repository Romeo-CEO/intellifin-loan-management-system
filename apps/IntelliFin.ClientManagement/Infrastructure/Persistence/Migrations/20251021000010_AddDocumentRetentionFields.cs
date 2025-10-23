using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration to add document retention and archival fields
/// Story 1.16: Document Retention Automation
/// </summary>
public partial class AddDocumentRetentionFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ArchivedBy",
            table: "ClientDocuments",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ArchivalReason",
            table: "ClientDocuments",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "CanRestore",
            table: "ClientDocuments",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "RestoredAt",
            table: "ClientDocuments",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RestoredBy",
            table: "ClientDocuments",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        // Update RetentionUntil comment to reflect 10-year policy
        // (No structural change needed, just documentation update)
        
        // Create index on archival status and retention date for efficient querying
        migrationBuilder.CreateIndex(
            name: "IX_ClientDocuments_RetentionUntil_IsArchived",
            table: "ClientDocuments",
            columns: new[] { "RetentionUntil", "IsArchived" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ClientDocuments_RetentionUntil_IsArchived",
            table: "ClientDocuments");

        migrationBuilder.DropColumn(
            name: "ArchivedBy",
            table: "ClientDocuments");

        migrationBuilder.DropColumn(
            name: "ArchivalReason",
            table: "ClientDocuments");

        migrationBuilder.DropColumn(
            name: "CanRestore",
            table: "ClientDocuments");

        migrationBuilder.DropColumn(
            name: "RestoredAt",
            table: "ClientDocuments");

        migrationBuilder.DropColumn(
            name: "RestoredBy",
            table: "ClientDocuments");
    }
}
