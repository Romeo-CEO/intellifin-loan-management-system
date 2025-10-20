using FluentValidation;
using System.Text.RegularExpressions;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Validator for CreateClientRequest
/// </summary>
public partial class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    [GeneratedRegex(@"^\d{6}/\d{2}/\d$")]
    private static partial Regex NrcFormatRegex();

    [GeneratedRegex(@"^\+260\d{9}$")]
    private static partial Regex PhoneFormatRegex();

    public CreateClientRequestValidator()
    {
        // NRC Validation
        RuleFor(x => x.Nrc)
            .NotEmpty().WithMessage("NRC is required")
            .Length(11).WithMessage("NRC must be exactly 11 characters")
            .Matches(NrcFormatRegex()).WithMessage("NRC must be in format XXXXXX/XX/X");

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

        // Date of Birth Validation
        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required")
            .LessThan(DateTime.Now).WithMessage("Date of birth cannot be in the future")
            .Must(BeAtLeast18YearsOld).WithMessage("Client must be at least 18 years old");

        // Gender Validation
        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required")
            .Must(g => new[] { "M", "F", "Other" }.Contains(g)).WithMessage("Gender must be M, F, or Other");

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

        // Branch ID Validation
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required");

        // Employment Fields (optional)
        RuleFor(x => x.EmployerType)
            .Must(t => t == null || new[] { "Government", "Private", "Self" }.Contains(t))
            .WithMessage("Employer type must be Government, Private, or Self");

        RuleFor(x => x.EmploymentStatus)
            .Must(s => s == null || new[] { "Active", "Suspended", "Terminated" }.Contains(s))
            .WithMessage("Employment status must be Active, Suspended, or Terminated");
    }

    private static bool BeAtLeast18YearsOld(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return age >= 18;
    }
}
