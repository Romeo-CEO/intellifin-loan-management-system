using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDualControlConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CHECK constraint to enforce dual-control verification
            // Ensures VerifiedBy is either NULL or different from UploadedBy
            // This provides database-level enforcement even if application logic is bypassed
            migrationBuilder.Sql(@"
                ALTER TABLE ClientDocuments
                ADD CONSTRAINT CK_ClientDocuments_DualControl
                CHECK (VerifiedBy IS NULL OR VerifiedBy <> UploadedBy);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove CHECK constraint
            migrationBuilder.Sql(@"
                ALTER TABLE ClientDocuments
                DROP CONSTRAINT IF EXISTS CK_ClientDocuments_DualControl;
            ");
        }
    }
}
