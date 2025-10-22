using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiskProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskRating = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RiskScore = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ComputedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RiskRulesVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RiskRulesChecksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuleExecutionLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputFactorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskProfiles_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.CheckConstraint("CK_RiskProfiles_RiskScore", "[RiskScore] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_RiskProfiles_RiskRating", "[RiskRating] IN ('Low', 'Medium', 'High')");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_RiskProfiles_ClientId_Current",
                table: "RiskProfiles",
                columns: new[] { "ClientId", "IsCurrent" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProfiles_ComputedAt",
                table: "RiskProfiles",
                column: "ComputedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProfiles_RiskRating",
                table: "RiskProfiles",
                column: "RiskRating");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProfiles_RiskScore",
                table: "RiskProfiles",
                column: "RiskScore");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProfiles_RiskRulesVersion",
                table: "RiskProfiles",
                column: "RiskRulesVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskProfiles");
        }
    }
}
