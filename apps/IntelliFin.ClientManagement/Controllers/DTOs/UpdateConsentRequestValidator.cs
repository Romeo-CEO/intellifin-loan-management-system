using FluentValidation;
using IntelliFin.ClientManagement.Domain.Entities;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Validator for UpdateConsentRequest
/// </summary>
public class UpdateConsentRequestValidator : AbstractValidator<UpdateConsentRequest>
{
    public UpdateConsentRequestValidator()
    {
        RuleFor(x => x.ConsentType)
            .NotEmpty()
            .WithMessage("Consent type is required")
            .Must(BeValidConsentType)
            .WithMessage("Consent type must be one of: Marketing, Operational, Regulatory");

        // Note: Regulatory consent cannot be revoked
        RuleFor(x => x)
            .Must(x => !IsRegulatoryConsentRevocation(x))
            .WithMessage("Regulatory consent cannot be disabled");

        // If all channels are disabled, require revocation reason
        RuleFor(x => x.RevocationReason)
            .NotEmpty()
            .When(x => !x.SmsEnabled && !x.EmailEnabled && !x.InAppEnabled && !x.CallEnabled)
            .WithMessage("Revocation reason is required when disabling all channels");
    }

    private static bool BeValidConsentType(string consentType)
    {
        var validTypes = new[] { ConsentType.Marketing, ConsentType.Operational, ConsentType.Regulatory };
        return validTypes.Contains(consentType, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsRegulatoryConsentRevocation(UpdateConsentRequest request)
    {
        // Regulatory consent cannot be completely disabled
        return request.ConsentType.Equals(ConsentType.Regulatory, StringComparison.OrdinalIgnoreCase)
            && !request.SmsEnabled
            && !request.EmailEnabled
            && !request.InAppEnabled
            && !request.CallEnabled;
    }
}
