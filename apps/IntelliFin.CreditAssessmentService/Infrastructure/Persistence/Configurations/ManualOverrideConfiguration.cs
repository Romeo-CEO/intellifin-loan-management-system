using IntelliFin.CreditAssessmentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Persistence.Configurations;

internal sealed class ManualOverrideConfiguration : IEntityTypeConfiguration<ManualOverride>
{
    public void Configure(EntityTypeBuilder<ManualOverride> builder)
    {
        builder.ToTable("manual_overrides");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Officer).HasMaxLength(128);
        builder.Property(x => x.Reason).HasMaxLength(512);
        builder.Property(x => x.Outcome).HasMaxLength(256);
    }
}
