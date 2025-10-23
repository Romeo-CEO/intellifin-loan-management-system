using IntelliFin.CreditAssessmentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Persistence.Configurations;

internal sealed class CreditAssessmentConfiguration : IEntityTypeConfiguration<CreditAssessment>
{
    public void Configure(EntityTypeBuilder<CreditAssessment> builder)
    {
        builder.ToTable("credit_assessments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Decision).HasConversion<string>();
        builder.Property(x => x.RiskGrade).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.DecisionReason).HasMaxLength(1024);
        builder.Property(x => x.AssessedBy).HasMaxLength(256);
        builder.Property(x => x.VaultConfigVersion).HasMaxLength(128);

        builder.HasMany(x => x.Factors)
            .WithOne()
            .HasForeignKey(x => x.CreditAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Overrides)
            .WithOne()
            .HasForeignKey(x => x.CreditAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AuditTrail)
            .WithOne()
            .HasForeignKey(x => x.CreditAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.LoanApplicationId);
    }
}
