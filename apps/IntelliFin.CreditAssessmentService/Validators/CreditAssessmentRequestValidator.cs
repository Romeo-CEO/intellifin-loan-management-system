using FluentValidation;
using IntelliFin.CreditAssessmentService.Models;

namespace IntelliFin.CreditAssessmentService.Validators;

/// <summary>
/// Validates incoming credit assessment requests.
/// </summary>
public sealed class CreditAssessmentRequestValidator : AbstractValidator<CreditAssessmentRequestDto>
{
    public CreditAssessmentRequestValidator()
    {
        RuleFor(r => r.LoanApplicationId).NotEmpty();
        RuleFor(r => r.ClientId).NotEmpty();
        RuleFor(r => r.RequestedAmount).GreaterThan(0);
        RuleFor(r => r.TermMonths).InclusiveBetween(1, 180);
        RuleFor(r => r.InterestRate).InclusiveBetween(0m, 1m);
    }
}
