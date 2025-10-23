using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunicationConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SmsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CallEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ConsentGivenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsentGivenBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConsentRevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationConsents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConsents_ClientId_Active",
                table: "CommunicationConsents",
                columns: new[] { "ClientId", "ConsentRevokedAt" },
                filter: "[ConsentRevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConsents_ClientId_ConsentType",
                table: "CommunicationConsents",
                columns: new[] { "ClientId", "ConsentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationConsents_ConsentType",
                table: "CommunicationConsents",
                column: "ConsentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationConsents");
        }
    }
}
