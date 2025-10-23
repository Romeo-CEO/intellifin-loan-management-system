using IntelliFin.CreditAssessmentService.Models;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using MassTransit;

namespace IntelliFin.CreditAssessmentService.Infrastructure.Messaging;

/// <summary>
/// Handles incoming assessment requests from the workflow engine.
/// </summary>
public sealed class AssessmentRequestHandler : IConsumer<AssessmentRequestedEvent>
{
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly ILogger<AssessmentRequestHandler> _logger;

    public AssessmentRequestHandler(ICreditAssessmentService creditAssessmentService, ILogger<AssessmentRequestHandler> logger)
    {
        _creditAssessmentService = creditAssessmentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AssessmentRequestedEvent> context)
    {
        try
        {
            await _creditAssessmentService.AssessAsync(new CreditAssessmentRequestDto
            {
                LoanApplicationId = context.Message.LoanApplicationId,
                ClientId = context.Message.ClientId,
                RequestedAmount = context.Message.RequestedAmount,
                TermMonths = context.Message.TermMonths,
                InterestRate = context.Message.InterestRate
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process assessment request for loan application {LoanApplicationId}", context.Message.LoanApplicationId);
            throw;
        }
    }
}

/// <summary>
/// Message contract used to request an assessment.
/// </summary>
public sealed record AssessmentRequestedEvent(Guid LoanApplicationId, Guid ClientId, decimal RequestedAmount, int TermMonths, decimal InterestRate);
