using IntelliFin.ClientManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RiskProfile entity
/// Defines indexes and constraints for historical risk tracking
/// </summary>
public class RiskProfileConfiguration : IEntityTypeConfiguration<RiskProfile>
{
    public void Configure(EntityTypeBuilder<RiskProfile> builder)
    {
        builder.ToTable("RiskProfiles");

        // Primary key
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign key to Client
        builder.Property(r => r.ClientId)
            .IsRequired();

        builder.HasOne(r => r.Client)
            .WithMany() // Client can have multiple historical risk profiles
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Risk rating and score
        builder.Property(r => r.RiskRating)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.RiskScore)
            .IsRequired();

        // Timestamps
        builder.Property(r => r.ComputedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(r => r.ComputedBy)
            .IsRequired()
            .HasMaxLength(256);

        // Vault rules tracking
        builder.Property(r => r.RiskRulesVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.RiskRulesChecksum)
            .IsRequired()
            .HasMaxLength(128);

        // Audit fields (JSON)
        builder.Property(r => r.RuleExecutionLog)
            .HasColumnType("nvarchar(max)");

        builder.Property(r => r.InputFactorsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        // Historical tracking
        builder.Property(r => r.IsCurrent)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(r => r.SupersededAt);

        builder.Property(r => r.SupersededReason)
            .HasMaxLength(100);

        // Unique index: Only one current risk profile per client
        builder.HasIndex(r => new { r.ClientId, r.IsCurrent })
            .IsUnique()
            .HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("UQ_RiskProfiles_ClientId_Current");

        // Index for historical queries
        builder.HasIndex(r => r.ComputedAt)
            .HasDatabaseName("IX_RiskProfiles_ComputedAt");

        // Index for risk rating queries
        builder.HasIndex(r => r.RiskRating)
            .HasDatabaseName("IX_RiskProfiles_RiskRating");

        // Index for risk score range queries
        builder.HasIndex(r => r.RiskScore)
            .HasDatabaseName("IX_RiskProfiles_RiskScore");

        // Index for rules version tracking
        builder.HasIndex(r => r.RiskRulesVersion)
            .HasDatabaseName("IX_RiskProfiles_RiskRulesVersion");

        // Check constraint for risk score range
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_RiskProfiles_RiskScore",
            "[RiskScore] BETWEEN 0 AND 100"));

        // Check constraint for valid risk ratings
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_RiskProfiles_RiskRating",
            "[RiskRating] IN ('Low', 'Medium', 'High')"));
    }
}
