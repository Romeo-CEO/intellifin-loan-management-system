using FluentValidation;
using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Validators;

/// <summary>
/// Validator for tenant creation requests
/// </summary>
public class TenantCreateRequestValidator : AbstractValidator<TenantCreateRequest>
{
    public TenantCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required.")
            .MaximumLength(200).WithMessage("Tenant name must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Tenant code is required.")
            .Length(3, 50).WithMessage("Tenant code must be between 3 and 50 characters.")
            .Matches("^[a-z0-9-]+$").WithMessage("Tenant code must contain only lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.Settings)
            .Must(BeValidJsonOrNull).WithMessage("Settings must be valid JSON or null.");
    }

    private static bool BeValidJsonOrNull(string? settings)
    {
        if (string.IsNullOrWhiteSpace(settings))
        {
            return true;
        }

        try
        {
            System.Text.Json.JsonDocument.Parse(settings);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Validator for user assignment requests
/// </summary>
public class UserAssignmentRequestValidator : AbstractValidator<UserAssignmentRequest>
{
    public UserAssignmentRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Role)
            .MaximumLength(100).WithMessage("Role must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Role));
    }
}
