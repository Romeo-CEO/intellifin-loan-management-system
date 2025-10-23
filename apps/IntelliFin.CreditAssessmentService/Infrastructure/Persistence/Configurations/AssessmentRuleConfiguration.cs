using IntelliFin.CreditAssessmentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentRuleConfiguration : IEntityTypeConfiguration<AssessmentRule>
{
    public void Configure(EntityTypeBuilder<AssessmentRule> builder)
    {
        builder.ToTable("assessment_rules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleKey).HasMaxLength(128);
        builder.Property(x => x.Expression).HasMaxLength(1024);
        builder.Property(x => x.Category).HasMaxLength(64);
    }
}
