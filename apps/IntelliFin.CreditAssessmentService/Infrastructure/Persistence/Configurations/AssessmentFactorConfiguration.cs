using IntelliFin.CreditAssessmentService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentFactorConfiguration : IEntityTypeConfiguration<AssessmentFactor>
{
    public void Configure(EntityTypeBuilder<AssessmentFactor> builder)
    {
        builder.ToTable("assessment_factors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(128);
        builder.Property(x => x.Impact).HasMaxLength(32);
        builder.Property(x => x.Explanation).HasMaxLength(1024);
    }
}
