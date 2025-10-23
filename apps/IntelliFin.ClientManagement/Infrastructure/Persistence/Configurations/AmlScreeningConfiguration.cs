using IntelliFin.ClientManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AmlScreening entity
/// </summary>
public class AmlScreeningConfiguration : IEntityTypeConfiguration<AmlScreening>
{
    public void Configure(EntityTypeBuilder<AmlScreening> builder)
    {
        builder.ToTable("AmlScreenings");

        // Primary key
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign key to KycStatus
        builder.HasOne(a => a.KycStatus)
            .WithMany()
            .HasForeignKey(a => a.KycStatusId)
            .OnDelete(DeleteBehavior.Cascade);

        // Required string properties
        builder.Property(a => a.ScreeningType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.ScreeningProvider)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("Manual");

        builder.Property(a => a.ScreenedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.RiskLevel)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Clear");

        // Optional string properties
        builder.Property(a => a.MatchDetails)
            .HasMaxLength(2000); // JSON data

        builder.Property(a => a.Notes)
            .HasMaxLength(1000);

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(100);

        // Boolean flags
        builder.Property(a => a.IsMatch)
            .IsRequired()
            .HasDefaultValue(false);

        // Timestamps
        builder.Property(a => a.ScreenedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Indexes for query performance
        builder.HasIndex(a => a.KycStatusId)
            .HasDatabaseName("IX_AmlScreenings_KycStatusId");

        builder.HasIndex(a => new { a.KycStatusId, a.ScreeningType })
            .HasDatabaseName("IX_AmlScreenings_KycStatusId_ScreeningType");

        builder.HasIndex(a => a.ScreenedAt)
            .HasDatabaseName("IX_AmlScreenings_ScreenedAt");
    }
}
