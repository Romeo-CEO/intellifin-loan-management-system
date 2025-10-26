using FluentValidation;

namespace IntelliFin.ClientManagement.Controllers.DTOs;

/// <summary>
/// Validator for UpdateKycStateRequest
/// </summary>
public class UpdateKycStateRequestValidator : AbstractValidator<UpdateKycStateRequest>
{
    public UpdateKycStateRequestValidator()
    {
        RuleFor(x => x.NewState)
            .NotEmpty()
            .WithMessage("New state is required")
            .Must(BeValidState)
            .WithMessage("Invalid state. Valid values: Pending, InProgress, Completed, EDD_Required, Rejected");

        RuleFor(x => x.EddReason)
            .NotEmpty()
            .When(x => x.NewState == "EDD_Required" || x.RequiresEdd == true)
            .WithMessage("EDD reason is required when escalating to EDD");

        RuleFor(x => x.EddReason)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.EddReason));

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }

    private bool BeValidState(string state)
    {
        var validStates = new[] { "Pending", "InProgress", "Completed", "EDD_Required", "Rejected" };
        return validStates.Contains(state);
    }
}
