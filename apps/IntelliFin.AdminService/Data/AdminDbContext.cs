using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.AdminService.Data;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AuditChainVerification> AuditChainVerifications => Set<AuditChainVerification>();
    public DbSet<SecurityIncident> SecurityIncidents => Set<SecurityIncident>();
    public DbSet<UserIdMapping> UserIdMappings => Set<UserIdMapping>();
    public DbSet<AuditArchiveMetadata> AuditArchiveMetadata => Set<AuditArchiveMetadata>();
    public DbSet<OfflineMergeHistory> OfflineMergeHistory => Set<OfflineMergeHistory>();
    public DbSet<ElevationRequest> ElevationRequests => Set<ElevationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.EventId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.Timestamp)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.Actor)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Action)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.EntityType)
                  .HasMaxLength(100);

            entity.Property(e => e.EntityId)
                  .HasMaxLength(100);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.IpAddress)
                  .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                  .HasMaxLength(500);

            entity.Property(e => e.MigrationSource)
                  .HasMaxLength(50);

            entity.Property(e => e.PreviousEventHash)
                  .HasMaxLength(64);

            entity.Property(e => e.CurrentEventHash)
                  .HasMaxLength(64);

            entity.Property(e => e.IntegrityStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("UNVERIFIED")
                  .IsRequired();

            entity.Property(e => e.IsGenesisEvent)
                  .HasDefaultValue(false);

            entity.Property(e => e.LastVerifiedAt);

            entity.Property(e => e.EventData)
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.IsOfflineEvent)
                  .HasDefaultValue(false);

            entity.Property(e => e.OfflineDeviceId)
                  .HasMaxLength(100);

            entity.Property(e => e.OfflineSessionId)
                  .HasMaxLength(100);

            entity.Property(e => e.OfflineMergeId);

            entity.Property(e => e.OriginalHash)
                  .HasMaxLength(64);

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("IX_AuditEvents_Timestamp");
            entity.HasIndex(e => e.Actor).HasDatabaseName("IX_AuditEvents_Actor");
            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("IX_AuditEvents_CorrelationId");
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("IX_AuditEvents_Entity");
            entity.HasIndex(e => e.EventId).IsUnique().HasDatabaseName("IX_AuditEvents_EventId");
            entity.HasIndex(e => new { e.Timestamp, e.CurrentEventHash }).HasDatabaseName("IX_AuditEvents_Timestamp_Hash");
            entity.HasIndex(e => e.OfflineMergeId).HasDatabaseName("IX_AuditEvents_OfflineMergeId");
        });

        modelBuilder.Entity<AuditChainVerification>(entity =>
        {
            entity.ToTable("AuditChainVerifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.VerificationId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.StartTime)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ChainStatus)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.InitiatedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.EndTime)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.VerificationId).HasDatabaseName("IX_AuditChainVerifications_VerificationId");
            entity.HasIndex(e => e.StartTime).HasDatabaseName("IX_AuditChainVerifications_StartTime");
        });

        modelBuilder.Entity<SecurityIncident>(entity =>
        {
            entity.ToTable("SecurityIncidents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IncidentId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.IncidentType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Severity)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.DetectedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ResolutionStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("OPEN")
                  .IsRequired();

            entity.Property(e => e.AffectedEntityType)
                  .HasMaxLength(100);

            entity.Property(e => e.AffectedEntityId)
                  .HasMaxLength(100);

            entity.Property(e => e.ResolvedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.DetectedAt).HasDatabaseName("IX_SecurityIncidents_DetectedAt");
            entity.HasIndex(e => e.IncidentType).HasDatabaseName("IX_SecurityIncidents_Type");
        });

        modelBuilder.Entity<UserIdMapping>(entity =>
        {
            entity.ToTable("UserIdMapping");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.AspNetUserId)
                  .HasMaxLength(450)
                  .IsRequired();

            entity.Property(e => e.KeycloakUserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.MigrationDate)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.AspNetUserId)
                  .IsUnique()
                  .HasDatabaseName("IX_AspNetUserId");

            entity.HasIndex(e => e.KeycloakUserId)
                  .IsUnique()
                  .HasDatabaseName("IX_KeycloakUserId");
        });

        modelBuilder.Entity<AuditArchiveMetadata>(entity =>
        {
            entity.ToTable("AuditArchiveMetadata");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ArchiveId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.FileName)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.ObjectKey)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.CompressionRatio)
                  .HasColumnType("decimal(5,2)")
                  .HasDefaultValue(0m);

            entity.Property(e => e.ChainStartHash)
                  .HasMaxLength(64);

            entity.Property(e => e.ChainEndHash)
                  .HasMaxLength(64);

            entity.Property(e => e.PreviousDayEndHash)
                  .HasMaxLength(64);

            entity.Property(e => e.StorageLocation)
                  .HasMaxLength(100)
                  .HasDefaultValue("PRIMARY")
                  .IsRequired();

            entity.Property(e => e.ReplicationStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("PENDING")
                  .IsRequired();

            entity.Property(e => e.LastAccessedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ExportDate).HasDatabaseName("IX_AuditArchiveMetadata_ExportDate");
            entity.HasIndex(e => new { e.EventDateStart, e.EventDateEnd }).HasDatabaseName("IX_AuditArchiveMetadata_EventDateRange");
            entity.HasIndex(e => e.ObjectKey).HasDatabaseName("IX_AuditArchiveMetadata_ObjectKey");
        });

        modelBuilder.Entity<OfflineMergeHistory>(entity =>
        {
            entity.ToTable("OfflineMergeHistory");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MergeId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.MergeTimestamp)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.DeviceId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.OfflineSessionId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.ErrorDetails)
                  .HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.MergeTimestamp).HasDatabaseName("IX_OfflineMergeHistory_MergeTimestamp");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_OfflineMergeHistory_UserId");
            entity.HasIndex(e => e.MergeId).IsUnique().HasDatabaseName("IX_OfflineMergeHistory_MergeId");
        });

        modelBuilder.Entity<ElevationRequest>(entity =>
        {
            entity.ToTable("ElevationRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.ElevationId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.RequestedRoles)
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(e => e.Justification)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.ManagerId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ManagerName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RejectedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(500);

            entity.Property(e => e.RevokedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RevocationReason)
                  .HasMaxLength(500);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ElevationId).IsUnique().HasDatabaseName("IX_ElevationRequests_ElevationId");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_ElevationRequests_UserId");
            entity.HasIndex(e => e.ManagerId).HasDatabaseName("IX_ElevationRequests_ManagerId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_ElevationRequests_Status");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("IX_ElevationRequests_ExpiresAt");
        });
    }
}
