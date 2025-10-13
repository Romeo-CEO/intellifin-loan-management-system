using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.AdminService.Migrations
{
    /// <inheritdoc />
    public partial class AddTamperEvidentChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntegrityStatus",
                table: "AuditEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UNVERIFIED");

            migrationBuilder.AddColumn<bool>(
                name: "IsGenesisEvent",
                table: "AuditEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVerifiedAt",
                table: "AuditEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Timestamp_Hash",
                table: "AuditEvents",
                columns: new[] { "Timestamp", "CurrentEventHash" });

            migrationBuilder.CreateTable(
                name: "AuditChainVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EventsVerified = table.Column<int>(type: "int", nullable: false),
                    ChainStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BrokenEventId = table.Column<long>(type: "bigint", nullable: true),
                    BrokenEventTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VerificationDurationMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditChainVerifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditChainVerifications_VerificationId",
                table: "AuditChainVerifications",
                column: "VerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditChainVerifications_StartTime",
                table: "AuditChainVerifications",
                column: "StartTime");

            migrationBuilder.CreateTable(
                name: "SecurityIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    IncidentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AffectedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AffectedEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResolutionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityIncidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityIncidents_DetectedAt",
                table: "SecurityIncidents",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityIncidents_IncidentType",
                table: "SecurityIncidents",
                column: "IncidentType");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[sp_InsertAuditEventsBatch]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[sp_InsertAuditEventsBatch];
END

IF TYPE_ID(N'[dbo].[AuditEventTableType]') IS NOT NULL
BEGIN
    DROP TYPE [dbo].[AuditEventTableType];
END

CREATE TYPE [dbo].[AuditEventTableType] AS TABLE
(
    [EventId] UNIQUEIDENTIFIER NOT NULL,
    [Timestamp] DATETIME2 NOT NULL,
    [Actor] NVARCHAR(100) NOT NULL,
    [Action] NVARCHAR(50) NOT NULL,
    [EntityType] NVARCHAR(100) NULL,
    [EntityId] NVARCHAR(100) NULL,
    [CorrelationId] NVARCHAR(100) NULL,
    [IpAddress] NVARCHAR(45) NULL,
    [UserAgent] NVARCHAR(500) NULL,
    [EventData] NVARCHAR(MAX) NULL,
    [PreviousEventHash] NVARCHAR(64) NULL,
    [CurrentEventHash] NVARCHAR(64) NULL,
    [IntegrityStatus] NVARCHAR(20) NOT NULL,
    [IsGenesisEvent] BIT NOT NULL,
    [LastVerifiedAt] DATETIME2 NULL
);

EXEC('CREATE PROCEDURE [dbo].[sp_InsertAuditEventsBatch]
    @Events [dbo].[AuditEventTableType] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[AuditEvents]
    (
        [EventId], [Timestamp], [Actor], [Action], [EntityType], [EntityId],
        [CorrelationId], [IpAddress], [UserAgent], [EventData],
        [PreviousEventHash], [CurrentEventHash], [IntegrityStatus], [IsGenesisEvent], [LastVerifiedAt]
    )
    SELECT
        [EventId], [Timestamp], [Actor], [Action], [EntityType], [EntityId],
        [CorrelationId], [IpAddress], [UserAgent], [EventData],
        [PreviousEventHash], [CurrentEventHash], [IntegrityStatus], [IsGenesisEvent], [LastVerifiedAt]
    FROM @Events;

    SELECT @@ROWCOUNT AS InsertedCount;
END');

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Batch insert entry point for Admin Service audit ingestion',
    @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'PROCEDURE',@level1name=N'sp_InsertAuditEventsBatch';

WITH FirstEvent AS (
    SELECT TOP(1) Id
    FROM [dbo].[AuditEvents]
    ORDER BY [Timestamp] ASC, [Id] ASC
)
UPDATE [dbo].[AuditEvents]
SET [IsGenesisEvent] = CASE WHEN [AuditEvents].[Id] IN (SELECT Id FROM FirstEvent) THEN 1 ELSE [IsGenesisEvent] END,
    [PreviousEventHash] = CASE WHEN [AuditEvents].[Id] IN (SELECT Id FROM FirstEvent) THEN NULL ELSE [PreviousEventHash] END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[sp_InsertAuditEventsBatch]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[sp_InsertAuditEventsBatch];
END

IF TYPE_ID(N'[dbo].[AuditEventTableType]') IS NOT NULL
BEGIN
    DROP TYPE [dbo].[AuditEventTableType];
END

CREATE TYPE [dbo].[AuditEventTableType] AS TABLE
(
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

EXEC('CREATE PROCEDURE [dbo].[sp_InsertAuditEventsBatch]
    @Events [dbo].[AuditEventTableType] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[AuditEvents]
    (
        [EventId], [Timestamp], [Actor], [Action], [EntityType], [EntityId],
        [CorrelationId], [IpAddress], [UserAgent], [EventData]
    )
    SELECT
        [EventId], [Timestamp], [Actor], [Action], [EntityType], [EntityId],
        [CorrelationId], [IpAddress], [UserAgent], [EventData]
    FROM @Events;

    SELECT @@ROWCOUNT AS InsertedCount;
END');

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Batch insert entry point for Admin Service audit ingestion',
    @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'PROCEDURE',@level1name=N'sp_InsertAuditEventsBatch';
");

            migrationBuilder.DropTable(
                name: "AuditChainVerifications");

            migrationBuilder.DropTable(
                name: "SecurityIncidents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Timestamp_Hash",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IntegrityStatus",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "IsGenesisEvent",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "LastVerifiedAt",
                table: "AuditEvents");
        }
    }
}
