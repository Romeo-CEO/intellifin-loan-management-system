using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAuditSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AuditEvents",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditEvents",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MigrationSource",
                table: "AuditEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuditEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Timestamp",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_Actor",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_CorrelationId",
                table: "AuditEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Timestamp",
                table: "AuditEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Actor",
                table: "AuditEvents",
                column: "Actor");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Entity",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventId",
                table: "AuditEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.Sql(@"
IF TYPE_ID(N'[dbo].[AuditEventTableType]') IS NULL
BEGIN
    CREATE TYPE [dbo].[AuditEventTableType] AS TABLE(
        [EventId] UNIQUEIDENTIFIER NOT NULL,
        [Timestamp] DATETIME2 NOT NULL,
        [Actor] NVARCHAR(100) NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [EntityType] NVARCHAR(100) NULL,
        [EntityId] NVARCHAR(100) NULL,
        [CorrelationId] NVARCHAR(100) NULL,
        [IpAddress] NVARCHAR(45) NULL,
        [UserAgent] NVARCHAR(500) NULL,
        [EventData] NVARCHAR(MAX) NULL
    );
END

IF OBJECT_ID(N'[dbo].[sp_InsertAuditEventsBatch]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[sp_InsertAuditEventsBatch];
END

EXEC('CREATE PROCEDURE [dbo].[sp_InsertAuditEventsBatch]
    @Events [dbo].[AuditEventTableType] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[AuditEvents]
    (
        [EventId],
        [Timestamp],
        [Actor],
        [Action],
        [EntityType],
        [EntityId],
        [CorrelationId],
        [IpAddress],
        [UserAgent],
        [EventData]
    )
    SELECT
        [EventId],
        [Timestamp],
        [Actor],
        [Action],
        [EntityType],
        [EntityId],
        [CorrelationId],
        [IpAddress],
        [UserAgent],
        [EventData]
    FROM @Events;

    SELECT @@ROWCOUNT AS InsertedCount;
END');

IF OBJECT_ID(N'[dbo].[sp_AuditEvents_BufferMetrics]') IS NULL
BEGIN
    EXEC('CREATE PROCEDURE [dbo].[sp_AuditEvents_BufferMetrics] AS BEGIN SET NOCOUNT ON; SELECT COUNT(1) AS Total FROM [dbo].[AuditEvents]; END');
END

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Batch insert entry point for Admin Service audit ingestion',
    @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'PROCEDURE',@level1name=N'sp_InsertAuditEventsBatch';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[sp_AuditEvents_BufferMetrics]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[sp_AuditEvents_BufferMetrics];
END

IF OBJECT_ID(N'[dbo].[sp_InsertAuditEventsBatch]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[sp_InsertAuditEventsBatch];
END

IF TYPE_ID(N'[dbo].[AuditEventTableType]') IS NOT NULL
BEGIN
    DROP TYPE [dbo].[AuditEventTableType];
END");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Actor",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Entity",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_EventId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Timestamp",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "MigrationSource",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AuditEvents");

            migrationBuilder.CreateIndex(
                name: "IX_Actor",
                table: "AuditEvents",
                column: "Actor");

            migrationBuilder.CreateIndex(
                name: "IX_CorrelationId",
                table: "AuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Timestamp",
                table: "AuditEvents",
                column: "Timestamp");
        }
    }
}
