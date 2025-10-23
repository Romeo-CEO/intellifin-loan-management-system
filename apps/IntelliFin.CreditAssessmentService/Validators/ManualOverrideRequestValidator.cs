using FluentValidation;
using IntelliFin.CreditAssessmentService.Models;

namespace IntelliFin.CreditAssessmentService.Validators;

/// <summary>
/// Validates manual override requests.
/// </summary>
public sealed class ManualOverrideRequestValidator : AbstractValidator<ManualOverrideRequestDto>
{
    public ManualOverrideRequestValidator()
    {
        RuleFor(r => r.Officer).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(512);
        RuleFor(r => r.Outcome).NotEmpty().MaximumLength(64);
    }
}
