using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.ClientManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for ClientDocument entity
/// </summary>
public class ClientDocumentConfiguration : IEntityTypeConfiguration<ClientDocument>
{
    public void Configure(EntityTypeBuilder<ClientDocument> builder)
    {
        builder.ToTable("ClientDocuments");

        // Primary Key
        builder.HasKey(d => d.Id);

        // Foreign Key to Client
        builder.HasOne(d => d.Client)
            .WithMany(c => c.Documents)
            .HasForeignKey(d => d.ClientId)
            .OnDelete(DeleteBehavior.Cascade); // Delete documents when client is deleted

        // Required Fields with Max Lengths
        builder.Property(d => d.DocumentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.BucketName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileSizeBytes)
            .IsRequired();

        builder.Property(d => d.FileHashSha256)
            .IsRequired()
            .HasMaxLength(64); // SHA256 produces 64 hex characters

        builder.Property(d => d.UploadStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>() // Convert enum to string for database storage
            .HasDefaultValue(UploadStatus.Uploaded);

        builder.Property(d => d.UploadedAt)
            .IsRequired();

        builder.Property(d => d.UploadedBy)
            .IsRequired()
            .HasMaxLength(255);

        // Optional Fields
        builder.Property(d => d.VerifiedBy)
            .HasMaxLength(255);

        builder.Property(d => d.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(d => d.CamundaProcessInstanceId)
            .HasMaxLength(255);

        builder.Property(d => d.ExtractedDataJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(d => d.CorrelationId)
            .HasMaxLength(255);

        builder.Property(d => d.RetentionUntil)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        // Indexes for Performance
        builder.HasIndex(d => d.ClientId)
            .HasDatabaseName("IX_ClientDocuments_ClientId");

        builder.HasIndex(d => d.UploadStatus)
            .HasDatabaseName("IX_ClientDocuments_UploadStatus");

        builder.HasIndex(d => d.ExpiryDate)
            .HasDatabaseName("IX_ClientDocuments_ExpiryDate")
            .HasFilter("[ExpiryDate] IS NOT NULL"); // Filtered index for performance

        builder.HasIndex(d => d.RetentionUntil)
            .HasDatabaseName("IX_ClientDocuments_RetentionUntil");

        builder.HasIndex(d => d.DocumentType)
            .HasDatabaseName("IX_ClientDocuments_DocumentType");

        // Composite index for common queries
        builder.HasIndex(d => new { d.ClientId, d.UploadStatus })
            .HasDatabaseName("IX_ClientDocuments_ClientId_UploadStatus");

        // Default Values
        builder.Property(d => d.IsArchived)
            .HasDefaultValue(false);
    }
}
