using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BucketName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileHashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Uploaded"),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CamundaProcessInstanceId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetentionUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtractedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OcrConfidenceScore = table.Column<float>(type: "real", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_ClientId",
                table: "ClientDocuments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_ClientId_UploadStatus",
                table: "ClientDocuments",
                columns: new[] { "ClientId", "UploadStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_DocumentType",
                table: "ClientDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_ExpiryDate",
                table: "ClientDocuments",
                column: "ExpiryDate",
                filter: "[ExpiryDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_RetentionUntil",
                table: "ClientDocuments",
                column: "RetentionUntil");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDocuments_UploadStatus",
                table: "ClientDocuments",
                column: "UploadStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientDocuments");
        }
    }
}
