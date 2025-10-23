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

    /// <summary>
    /// Client version history (Story 1.4)
    /// </summary>
    public DbSet<ClientVersion> ClientVersions => Set<ClientVersion>();

    /// <summary>
    /// Client documents (Story 1.6)
    /// </summary>
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();

    /// <summary>
    /// Communication consents (Story 1.7)
    /// </summary>
    public DbSet<CommunicationConsent> CommunicationConsents => Set<CommunicationConsent>();

    /// <summary>
    /// KYC statuses (Story 1.10)
    /// </summary>
    public DbSet<KycStatus> KycStatuses => Set<KycStatus>();

    /// <summary>
    /// AML screenings (Story 1.11)
    /// </summary>
    public DbSet<AmlScreening> AmlScreenings => Set<AmlScreening>();

    public DbSet<RiskProfile> RiskProfiles => Set<RiskProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ClientVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ClientDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new CommunicationConsentConfiguration());
        modelBuilder.ApplyConfiguration(new KycStatusConfiguration());
        modelBuilder.ApplyConfiguration(new AmlScreeningConfiguration());
        modelBuilder.ApplyConfiguration(new RiskProfileConfiguration());
        
        // Future entity configurations will be added here as needed
    }
}
