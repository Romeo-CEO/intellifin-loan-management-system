using FluentValidation;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Validator for VerifyDocumentRequest
/// Ensures rejection reason provided when document is rejected
/// </summary>
public class VerifyDocumentRequestValidator : AbstractValidator<VerifyDocumentRequest>
{
    public VerifyDocumentRequestValidator()
    {
        // If document is being rejected (Approved = false), rejection reason is required
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .When(x => !x.Approved)
            .WithMessage("Rejection reason is required when rejecting a document");

        // Rejection reason max length
        RuleFor(x => x.RejectionReason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.RejectionReason))
            .WithMessage("Rejection reason cannot exceed 500 characters");
    }
}
