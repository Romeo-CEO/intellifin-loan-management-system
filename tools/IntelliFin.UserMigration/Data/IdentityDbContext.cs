using IntelliFin.UserMigration.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.UserMigration.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<AspNetUser> Users => Set<AspNetUser>();
    public DbSet<AspNetRole> Roles => Set<AspNetRole>();
    public DbSet<AspNetUserRole> UserRoles => Set<AspNetUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AspNetUser>(builder =>
        {
            builder.ToTable("AspNetUsers");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.UserName).HasMaxLength(256);
            builder.Property(u => u.NormalizedUserName).HasMaxLength(256);
            builder.Property(u => u.Email).HasMaxLength(256);
            builder.Property(u => u.NormalizedEmail).HasMaxLength(256);
            builder.Property(u => u.FirstName).HasMaxLength(256);
            builder.Property(u => u.LastName).HasMaxLength(256);
            builder.Property(u => u.PhoneNumber).HasMaxLength(32);
            builder.HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();
        });

        modelBuilder.Entity<AspNetRole>(builder =>
        {
            builder.ToTable("AspNetRoles");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Name).HasMaxLength(256);
            builder.Property(r => r.NormalizedName).HasMaxLength(256);
            builder.Property(r => r.Description).HasMaxLength(512);
            builder.HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();
        });

        modelBuilder.Entity<AspNetUserRole>(builder =>
        {
            builder.ToTable("AspNetUserRoles");
            builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        });
    }
}
