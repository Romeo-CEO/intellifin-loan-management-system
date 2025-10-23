using IntelliFin.CreditAssessmentService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentAuditTrailConfiguration : IEntityTypeConfiguration<AssessmentAuditTrail>
{
    public void Configure(EntityTypeBuilder<AssessmentAuditTrail> builder)
    {
        builder.ToTable("assessment_audit_trail");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Actor).HasMaxLength(128);
        builder.Property(x => x.Action).HasMaxLength(128);
        builder.Property(x => x.Details).HasMaxLength(1024);
    }
}
