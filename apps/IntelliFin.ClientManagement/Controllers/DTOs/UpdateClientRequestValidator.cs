using FluentValidation;
using System.Text.RegularExpressions;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Validator for UpdateClientRequest
/// </summary>
public partial class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    [GeneratedRegex(@"^\+260\d{9}$")]
    private static partial Regex PhoneFormatRegex();

    public UpdateClientRequestValidator()
    {
        // Name Validation
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.OtherNames)
            .MaximumLength(100).WithMessage("Other names must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.OtherNames));

        // Marital Status Validation
        RuleFor(x => x.MaritalStatus)
            .NotEmpty().WithMessage("Marital status is required")
            .Must(s => new[] { "Single", "Married", "Divorced", "Widowed" }.Contains(s))
            .WithMessage("Marital status must be Single, Married, Divorced, or Widowed");

        // Contact Validation
        RuleFor(x => x.PrimaryPhone)
            .NotEmpty().WithMessage("Primary phone is required")
            .Matches(PhoneFormatRegex()).WithMessage("Primary phone must be in Zambian format (+260XXXXXXXXX)");

        RuleFor(x => x.SecondaryPhone)
            .Matches(PhoneFormatRegex()).WithMessage("Secondary phone must be in Zambian format (+260XXXXXXXXX)")
            .When(x => !string.IsNullOrEmpty(x.SecondaryPhone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.PhysicalAddress)
            .NotEmpty().WithMessage("Physical address is required")
            .MaximumLength(500).WithMessage("Physical address must not exceed 500 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters");

        RuleFor(x => x.Province)
            .NotEmpty().WithMessage("Province is required")
            .MaximumLength(100).WithMessage("Province must not exceed 100 characters");

        // Employment Fields (optional)
        RuleFor(x => x.EmployerType)
            .Must(t => t == null || new[] { "Government", "Private", "Self" }.Contains(t))
            .WithMessage("Employer type must be Government, Private, or Self");

        RuleFor(x => x.EmploymentStatus)
            .Must(s => s == null || new[] { "Active", "Suspended", "Terminated" }.Contains(s))
            .WithMessage("Employment status must be Active, Suspended, or Terminated");
    }
}
