using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.AdminService.Data;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<UserIdMapping> UserIdMappings => Set<UserIdMapping>();

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

            entity.Property(e => e.PreviousEventHash)
                  .HasMaxLength(64);

            entity.Property(e => e.CurrentEventHash)
                  .HasMaxLength(64);

            entity.Property(e => e.EventData)
                  .HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("IX_Timestamp");
            entity.HasIndex(e => e.Actor).HasDatabaseName("IX_Actor");
            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("IX_CorrelationId");
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
    }
}
