using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Initial migration - empty database schema
            // Entity tables will be added in subsequent stories:
            // - Story 1.3: Clients table
            // - Story 1.4: ClientVersions table  
            // - Story 1.6: ClientDocuments table
            // - Story 1.7: CommunicationConsents table
            // - Story 1.13: RiskProfiles, AmlScreenings tables
            // - Story 1.15: ClientEvents table
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No tables to drop in initial migration
        }
    }
}
