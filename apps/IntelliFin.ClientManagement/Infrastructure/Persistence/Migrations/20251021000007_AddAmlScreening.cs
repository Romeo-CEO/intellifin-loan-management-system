using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAmlScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmlScreenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    KycStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScreeningType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScreeningProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Manual"),
                    ScreenedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ScreenedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsMatch = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MatchDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RiskLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Clear"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmlScreenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmlScreenings_KycStatuses_KycStatusId",
                        column: x => x.KycStatusId,
                        principalTable: "KycStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmlScreenings_KycStatusId",
                table: "AmlScreenings",
                column: "KycStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AmlScreenings_KycStatusId_ScreeningType",
                table: "AmlScreenings",
                columns: new[] { "KycStatusId", "ScreeningType" });

            migrationBuilder.CreateIndex(
                name: "IX_AmlScreenings_ScreenedAt",
                table: "AmlScreenings",
                column: "ScreenedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmlScreenings");
        }
    }
}
