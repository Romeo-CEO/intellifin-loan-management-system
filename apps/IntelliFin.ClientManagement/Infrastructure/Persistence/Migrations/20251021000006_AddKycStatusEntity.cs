using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKycStatusEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KycStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KycStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KycCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KycCompletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    HasNrc = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasProofOfAddress = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasPayslip = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasEmploymentLetter = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDocumentComplete = table.Column<bool>(type: "bit", nullable: false, 
                        computedColumnSql: "CASE WHEN [HasNrc] = 1 AND [HasProofOfAddress] = 1 AND ([HasPayslip] = 1 OR [HasEmploymentLetter] = 1) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END", 
                        stored: true),
                    AmlScreeningComplete = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AmlScreenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmlScreenedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RequiresEdd = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EddReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EddEscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EddReportObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EddApprovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EddCeoApprovedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EddApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KycStatuses_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KycStatuses_ClientId_CurrentState",
                table: "KycStatuses",
                columns: new[] { "ClientId", "CurrentState" });

            migrationBuilder.CreateIndex(
                name: "IX_KycStatuses_KycStartedAt",
                table: "KycStatuses",
                column: "KycStartedAt");

            migrationBuilder.CreateIndex(
                name: "UQ_KycStatuses_ClientId",
                table: "KycStatuses",
                column: "ClientId",
                unique: true);

            // Add CHECK constraint for valid states
            migrationBuilder.Sql(@"
                ALTER TABLE KycStatuses
                ADD CONSTRAINT CK_KycStatuses_CurrentState
                CHECK (CurrentState IN ('Pending', 'InProgress', 'Completed', 'EDD_Required', 'Rejected'));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop CHECK constraint
            migrationBuilder.Sql(@"
                ALTER TABLE KycStatuses
                DROP CONSTRAINT IF EXISTS CK_KycStatuses_CurrentState;
            ");

            migrationBuilder.DropTable(
                name: "KycStatuses");
        }
    }
}
