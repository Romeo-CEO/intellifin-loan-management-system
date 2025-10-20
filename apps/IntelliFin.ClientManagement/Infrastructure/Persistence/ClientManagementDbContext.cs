using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence;

/// <summary>
/// Database context for Client Management service
/// </summary>
public class ClientManagementDbContext : DbContext
{
    public ClientManagementDbContext(DbContextOptions<ClientManagementDbContext> options) 
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Entity configurations will be added here in future stories
        // Story 1.3: Client CRUD Operations will add Client entity
        // Story 1.4: Client Versioning will add ClientVersion entity
        // Story 1.6: KycDocument Integration will add ClientDocument entity
        // Story 1.7: Communications Integration will add CommunicationConsent entity
    }
}
