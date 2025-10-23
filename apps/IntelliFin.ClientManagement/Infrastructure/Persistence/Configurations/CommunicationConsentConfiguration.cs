using IntelliFin.ClientManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for CommunicationConsent entity
/// </summary>
public class CommunicationConsentConfiguration : IEntityTypeConfiguration<CommunicationConsent>
{
    public void Configure(EntityTypeBuilder<CommunicationConsent> builder)
    {
        builder.ToTable("CommunicationConsents");

        // Primary Key
        builder.HasKey(c => c.Id);

        // Foreign Key to Client
        builder.HasOne(c => c.Client)
            .WithMany(cl => cl.Consents)
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Cascade); // Delete consents when client is deleted

        // Required Fields with Max Lengths
        builder.Property(c => c.ConsentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.SmsEnabled)
            .IsRequired();

        builder.Property(c => c.EmailEnabled)
            .IsRequired();

        builder.Property(c => c.InAppEnabled)
            .IsRequired();

        builder.Property(c => c.CallEnabled)
            .IsRequired();

        builder.Property(c => c.ConsentGivenAt)
            .IsRequired();

        builder.Property(c => c.ConsentGivenBy)
            .IsRequired()
            .HasMaxLength(50);

        // Optional Fields
        builder.Property(c => c.RevocationReason)
            .HasMaxLength(500);

        builder.Property(c => c.CorrelationId)
            .HasMaxLength(255);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Indexes for Performance
        // Composite index for fast consent lookup by client and type
        builder.HasIndex(c => new { c.ClientId, c.ConsentType })
            .HasDatabaseName("IX_CommunicationConsents_ClientId_ConsentType")
            .IsUnique(); // One consent record per client per type

        // Index on ConsentType for filtering
        builder.HasIndex(c => c.ConsentType)
            .HasDatabaseName("IX_CommunicationConsents_ConsentType");

        // Filtered index for active consents (not revoked)
        builder.HasIndex(c => new { c.ClientId, c.ConsentRevokedAt })
            .HasDatabaseName("IX_CommunicationConsents_ClientId_Active")
            .HasFilter("[ConsentRevokedAt] IS NULL");

        // Default Values
        builder.Property(c => c.SmsEnabled)
            .HasDefaultValue(false);

        builder.Property(c => c.EmailEnabled)
            .HasDefaultValue(false);

        builder.Property(c => c.InAppEnabled)
            .HasDefaultValue(false);

        builder.Property(c => c.CallEnabled)
            .HasDefaultValue(false);

        // Computed Properties (not mapped to database)
        builder.Ignore(c => c.IsActive);
    }
}
