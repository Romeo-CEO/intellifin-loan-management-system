using FluentValidation;
using IntelliFin.CreditAssessmentService.Models.Requests;

namespace IntelliFin.CreditAssessmentService.Validators;

/// <summary>
/// Validator for credit assessment requests.
/// </summary>
public class AssessmentRequestValidator : AbstractValidator<AssessmentRequest>
{
    public AssessmentRequestValidator()
    {
        RuleFor(x => x.LoanApplicationId)
            .NotEmpty()
            .WithMessage("Loan Application ID is required");

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("Client ID is required");

        RuleFor(x => x.RequestedAmount)
            .GreaterThan(0)
            .WithMessage("Requested amount must be greater than 0")
            .LessThanOrEqualTo(10_000_000)
            .WithMessage("Requested amount cannot exceed 10,000,000 ZMW");

        RuleFor(x => x.TermMonths)
            .GreaterThan(0)
            .WithMessage("Term must be greater than 0 months")
            .LessThanOrEqualTo(360)
            .WithMessage("Term cannot exceed 360 months (30 years)");

        RuleFor(x => x.ProductType)
            .NotEmpty()
            .WithMessage("Product type is required")
            .Must(BeValidProductType)
            .WithMessage("Product type must be PAYROLL or BUSINESS");

        // PAYROLL loans require EmployerId
        RuleFor(x => x.EmployerId)
            .NotEmpty()
            .When(x => x.ProductType == "PAYROLL")
            .WithMessage("Employer ID is required for PAYROLL loans");

        RuleFor(x => x.AssessmentContext)
            .Must(BeValidContext)
            .When(x => !string.IsNullOrEmpty(x.AssessmentContext))
            .WithMessage("Assessment context must be Initial, Renewal, or Modification");
    }

    private static bool BeValidProductType(string? productType)
    {
        if (string.IsNullOrWhiteSpace(productType))
            return false;

        return productType == "PAYROLL" || productType == "BUSINESS";
    }

    private static bool BeValidContext(string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
            return true;

        return context is "Initial" or "Renewal" or "Modification";
    }
}
