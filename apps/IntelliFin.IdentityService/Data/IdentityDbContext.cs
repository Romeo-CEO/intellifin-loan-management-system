using IntelliFin.IdentityService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.IdentityService.Data;

/// <summary>
/// Entity Framework Core DbContext for the Identity Service.
/// This DbContext leverages ASP.NET Core Identity infrastructure while
/// leaving room for additional domain entities as the service evolves.
/// </summary>
public class IdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName)
                .HasMaxLength(200);

            entity.Property(u => u.LastName)
                .HasMaxLength(200);

            entity.Property(u => u.BranchId)
                .HasMaxLength(128);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(r => r.Description)
                .HasMaxLength(512);
        });
    }
}
