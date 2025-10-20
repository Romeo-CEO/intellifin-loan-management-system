using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;
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

    /// <summary>
    /// Clients (Story 1.3)
    /// </summary>
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        
        // Future entity configurations will be added here:
        // Story 1.4: Client Versioning will add ClientVersion entity
        // Story 1.6: KycDocument Integration will add ClientDocument entity
        // Story 1.7: Communications Integration will add CommunicationConsent entity
    }
}
