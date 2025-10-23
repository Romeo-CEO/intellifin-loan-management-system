using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the credit assessment service.
/// </summary>
public class CreditAssessmentDbContext : DbContext
{
    public CreditAssessmentDbContext(DbContextOptions<CreditAssessmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<CreditAssessment> CreditAssessments => Set<CreditAssessment>();
    public DbSet<AssessmentRule> AssessmentRules => Set<AssessmentRule>();
    public DbSet<AssessmentFactor> AssessmentFactors => Set<AssessmentFactor>();
    public DbSet<ManualOverride> ManualOverrides => Set<ManualOverride>();
    public DbSet<AssessmentAuditTrail> AssessmentAuditTrail => Set<AssessmentAuditTrail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreditAssessmentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
