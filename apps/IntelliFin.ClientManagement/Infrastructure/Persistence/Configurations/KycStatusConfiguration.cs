using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for KycStatus entity
/// Defines table structure, constraints, and computed columns
/// </summary>
public class KycStatusConfiguration : IEntityTypeConfiguration<KycStatus>
{
    public void Configure(EntityTypeBuilder<KycStatus> builder)
    {
        builder.ToTable("KycStatuses");

        // Primary key
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        // Foreign key to Client (One-to-One)
        builder.HasOne(k => k.Client)
            .WithOne(c => c.KycStatus)
            .HasForeignKey<KycStatus>(k => k.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint on ClientId (one KYC status per client)
        builder.HasIndex(k => k.ClientId)
            .IsUnique()
            .HasDatabaseName("UQ_KycStatuses_ClientId");

        // Enum to string conversion for CurrentState
        builder.Property(k => k.CurrentState)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        // String properties
        builder.Property(k => k.KycCompletedBy)
            .HasMaxLength(256);

        builder.Property(k => k.CamundaProcessInstanceId)
            .HasMaxLength(128);

        builder.Property(k => k.AmlScreenedBy)
            .HasMaxLength(256);

        builder.Property(k => k.EddReason)
            .HasMaxLength(100);

        builder.Property(k => k.EddReportObjectKey)
            .HasMaxLength(500);

        builder.Property(k => k.EddApprovedBy)
            .HasMaxLength(256);

        builder.Property(k => k.EddCeoApprovedBy)
            .HasMaxLength(256);

        // Required boolean flags with defaults
        builder.Property(k => k.HasNrc)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.HasProofOfAddress)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.HasPayslip)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.HasEmploymentLetter)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.AmlScreeningComplete)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(k => k.RequiresEdd)
            .IsRequired()
            .HasDefaultValue(false);

        // Computed column for IsDocumentComplete
        // This creates a SQL Server computed column
        builder.Property(k => k.IsDocumentComplete)
            .HasComputedColumnSql(
                "CASE WHEN [HasNrc] = 1 AND [HasProofOfAddress] = 1 AND ([HasPayslip] = 1 OR [HasEmploymentLetter] = 1) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
                stored: true);

        // Timestamps
        builder.Property(k => k.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(k => k.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Indexes for query performance
        builder.HasIndex(k => new { k.ClientId, k.CurrentState })
            .HasDatabaseName("IX_KycStatuses_ClientId_CurrentState");

        builder.HasIndex(k => k.KycStartedAt)
            .HasDatabaseName("IX_KycStatuses_KycStartedAt");
    }
}
