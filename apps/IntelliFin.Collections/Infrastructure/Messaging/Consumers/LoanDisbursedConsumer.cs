using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Infrastructure.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes LoanDisbursed events and generates repayment schedules.
/// Implements idempotent processing.
/// </summary>
public class LoanDisbursedConsumer : IConsumer<LoanDisbursed>
{
    private readonly IRepaymentScheduleService _scheduleService;
    private readonly ILogger<LoanDisbursedConsumer> _logger;

    public LoanDisbursedConsumer(
        IRepaymentScheduleService scheduleService,
        ILogger<LoanDisbursedConsumer> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LoanDisbursed> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Received LoanDisbursed event for loan {LoanId}, client {ClientId}, amount {Amount}",
            message.LoanId, message.ClientId, message.DisbursedAmount);

        try
        {
            var correlationId = message.CorrelationId ?? context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString();

            var scheduleId = await _scheduleService.GenerateScheduleAsync(
                loanId: message.LoanId,
                clientId: message.ClientId,
                productCode: message.ProductCode,
                principalAmount: message.DisbursedAmount,
                interestRate: message.InterestRate,
                termMonths: message.TermMonths,
                firstPaymentDate: message.FirstPaymentDate,
                correlationId: correlationId,
                generatedBy: "System",
                cancellationToken: context.CancellationToken);

            _logger.LogInformation(
                "Successfully generated repayment schedule {ScheduleId} for loan {LoanId}",
                scheduleId, message.LoanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate repayment schedule for loan {LoanId}",
                message.LoanId);
            
            throw; // Retry via MassTransit
        }
    }
}
