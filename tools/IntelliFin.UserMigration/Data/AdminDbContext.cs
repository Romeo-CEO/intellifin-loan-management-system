using IntelliFin.UserMigration.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.UserMigration.Data;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<UserIdMapping> UserIdMappings => Set<UserIdMapping>();
    public DbSet<RoleIdMapping> RoleIdMappings => Set<RoleIdMapping>();
    public DbSet<MigrationAuditLog> MigrationAuditLogs => Set<MigrationAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserIdMapping>(builder =>
        {
            builder.ToTable("UserIdMapping");
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.AspNetUserId).IsUnique();
            builder.HasIndex(m => m.KeycloakUserId).IsUnique();
            builder.Property(m => m.AspNetUserId).HasMaxLength(450).IsRequired();
            builder.Property(m => m.KeycloakUserId).HasMaxLength(100).IsRequired();
            builder.Property(m => m.MigrationStatus).HasMaxLength(50).IsRequired();
            builder.Property(m => m.MigrationDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(m => m.Notes).HasMaxLength(1024);
        });

        modelBuilder.Entity<RoleIdMapping>(builder =>
        {
            builder.ToTable("RoleIdMapping");
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.AspNetRoleId).IsUnique();
            builder.HasIndex(m => m.KeycloakRoleId).IsUnique();
            builder.Property(m => m.AspNetRoleId).HasMaxLength(450).IsRequired();
            builder.Property(m => m.KeycloakRoleId).HasMaxLength(100).IsRequired();
            builder.Property(m => m.RoleName).HasMaxLength(256).IsRequired();
            builder.Property(m => m.MigrationDate).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<MigrationAuditLog>(builder =>
        {
            builder.ToTable("MigrationAuditLog");
            builder.HasKey(log => log.Id);
            builder.Property(log => log.CreatedOnUtc).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(log => log.Action).HasMaxLength(128).IsRequired();
            builder.Property(log => log.Actor).HasMaxLength(256).IsRequired();
            builder.Property(log => log.Details).HasMaxLength(2048);
        });
    }
}
