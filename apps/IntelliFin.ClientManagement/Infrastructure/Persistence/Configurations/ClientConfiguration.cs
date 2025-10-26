using IntelliFin.ClientManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for Client entity
/// </summary>
public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        // Primary Key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        // Unique Indexes
        builder.HasIndex(c => c.Nrc)
            .IsUnique()
            .HasDatabaseName("IX_Clients_Nrc");

        builder.HasIndex(c => c.PayrollNumber)
            .IsUnique()
            .HasDatabaseName("IX_Clients_PayrollNumber")
            .HasFilter("[PayrollNumber] IS NOT NULL");

        // Required Fields with Max Lengths
        builder.Property(c => c.Nrc)
            .IsRequired()
            .HasMaxLength(11);

        builder.Property(c => c.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.OtherNames)
            .HasMaxLength(100);

        builder.Property(c => c.Gender)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.MaritalStatus)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.Nationality)
            .HasMaxLength(50);

        // Employment Fields
        builder.Property(c => c.PayrollNumber)
            .HasMaxLength(50);

        builder.Property(c => c.Ministry)
            .HasMaxLength(100);

        builder.Property(c => c.EmployerType)
            .HasMaxLength(20);

        builder.Property(c => c.EmploymentStatus)
            .HasMaxLength(20);

        // Contact Fields
        builder.Property(c => c.PrimaryPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.SecondaryPhone)
            .HasMaxLength(20);

        builder.Property(c => c.Email)
            .HasMaxLength(255);

        builder.Property(c => c.PhysicalAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Province)
            .IsRequired()
            .HasMaxLength(100);

        // Compliance Fields
        builder.Property(c => c.KycStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        builder.Property(c => c.KycCompletedBy)
            .HasMaxLength(100);

        builder.Property(c => c.AmlRiskLevel)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Low");

        builder.Property(c => c.IsPep)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.IsSanctioned)
            .IsRequired()
            .HasDefaultValue(false);

        // Risk Fields
        builder.Property(c => c.RiskRating)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Low");

        // Lifecycle Fields
        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Active");

        builder.Property(c => c.BranchId)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cv => cv.VersionNumber)
            .IsRequired()
            .HasDefaultValue(1);

        // Check Constraint
        builder.ToTable(t => t.HasCheckConstraint("CK_Clients_VersionNumber", "[VersionNumber] >= 1"));

        // Indexes for common queries
        builder.HasIndex(c => c.Status)
            .HasDatabaseName("IX_Clients_Status");

        builder.HasIndex(c => c.BranchId)
            .HasDatabaseName("IX_Clients_BranchId");

        builder.HasIndex(c => c.KycStatus)
            .HasDatabaseName("IX_Clients_KycStatus");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("IX_Clients_CreatedAt");
    }
}
