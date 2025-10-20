using IntelliFin.ClientManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for ClientVersion entity
/// Implements SCD-2 (Slowly Changing Dimension Type 2) temporal tracking
/// </summary>
public class ClientVersionConfiguration : IEntityTypeConfiguration<ClientVersion>
{
    public void Configure(EntityTypeBuilder<ClientVersion> builder)
    {
        builder.ToTable("ClientVersions");

        // Primary Key
        builder.HasKey(cv => cv.Id);
        builder.Property(cv => cv.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        // Foreign Key to Client
        builder.HasOne(cv => cv.Client)
            .WithMany()
            .HasForeignKey(cv => cv.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite Unique Index - No duplicate version numbers for same client
        builder.HasIndex(cv => new { cv.ClientId, cv.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_ClientVersions_ClientId_VersionNumber");

        // Temporal Query Index - Optimizes point-in-time queries
        builder.HasIndex(cv => new { cv.ClientId, cv.ValidFrom, cv.ValidTo })
            .HasDatabaseName("IX_ClientVersions_ClientId_ValidFrom_ValidTo");

        // Current Version Index - Fast lookup of active version
        builder.HasIndex(cv => new { cv.ClientId, cv.IsCurrent })
            .HasDatabaseName("IX_ClientVersions_ClientId_IsCurrent");

        // Unique Filtered Index - Ensures only one IsCurrent=true per ClientId
        builder.HasIndex(cv => cv.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_ClientVersions_ClientId_IsCurrent_Unique")
            .HasFilter("[IsCurrent] = 1");

        // Required Fields with Max Lengths (matching Client configuration)
        builder.Property(cv => cv.ClientId)
            .IsRequired();

        builder.Property(cv => cv.VersionNumber)
            .IsRequired();

        builder.Property(cv => cv.Nrc)
            .IsRequired()
            .HasMaxLength(11);

        builder.Property(cv => cv.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cv => cv.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cv => cv.OtherNames)
            .HasMaxLength(100);

        builder.Property(cv => cv.Gender)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(cv => cv.MaritalStatus)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cv => cv.Nationality)
            .HasMaxLength(50);

        // Employment Fields
        builder.Property(cv => cv.PayrollNumber)
            .HasMaxLength(50);

        builder.Property(cv => cv.Ministry)
            .HasMaxLength(100);

        builder.Property(cv => cv.EmployerType)
            .HasMaxLength(20);

        builder.Property(cv => cv.EmploymentStatus)
            .HasMaxLength(20);

        // Contact Fields
        builder.Property(cv => cv.PrimaryPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cv => cv.SecondaryPhone)
            .HasMaxLength(20);

        builder.Property(cv => cv.Email)
            .HasMaxLength(255);

        builder.Property(cv => cv.PhysicalAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(cv => cv.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cv => cv.Province)
            .IsRequired()
            .HasMaxLength(100);

        // Compliance Fields
        builder.Property(cv => cv.KycStatus)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cv => cv.KycCompletedBy)
            .HasMaxLength(100);

        builder.Property(cv => cv.AmlRiskLevel)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cv => cv.IsPep)
            .IsRequired();

        builder.Property(cv => cv.IsSanctioned)
            .IsRequired();

        // Risk Fields
        builder.Property(cv => cv.RiskRating)
            .IsRequired()
            .HasMaxLength(20);

        // Lifecycle Fields
        builder.Property(cv => cv.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(cv => cv.BranchId)
            .IsRequired();

        // Temporal Tracking Fields
        builder.Property(cv => cv.ValidFrom)
            .IsRequired();

        builder.Property(cv => cv.ValidTo)
            .IsRequired(false);

        builder.Property(cv => cv.IsCurrent)
            .IsRequired();

        // Change Tracking Fields
        builder.Property(cv => cv.ChangeSummaryJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(cv => cv.ChangeReason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(cv => cv.CreatedAt)
            .IsRequired();

        builder.Property(cv => cv.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cv => cv.IpAddress)
            .HasMaxLength(50);

        builder.Property(cv => cv.CorrelationId)
            .HasMaxLength(100);

        // Indexes for common queries
        builder.HasIndex(cv => cv.ValidFrom)
            .HasDatabaseName("IX_ClientVersions_ValidFrom");

        builder.HasIndex(cv => cv.CreatedAt)
            .HasDatabaseName("IX_ClientVersions_CreatedAt");
    }
}
