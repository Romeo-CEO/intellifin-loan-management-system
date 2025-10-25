using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Infrastructure.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes PMEC payment events and processes them.
/// </summary>
public class PmecPaymentReceivedConsumer : IConsumer<PmecPaymentReceived>
{
    private readonly IPaymentProcessingService _paymentService;
    private readonly ILogger<PmecPaymentReceivedConsumer> _logger;

    public PmecPaymentReceivedConsumer(
        IPaymentProcessingService paymentService,
        ILogger<PmecPaymentReceivedConsumer> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PmecPaymentReceived> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Received PMEC payment for loan {LoanId}, amount {Amount}, reference {Reference}",
            message.LoanId, message.Amount, message.PmecReference);

        try
        {
            var correlationId = message.CorrelationId ?? context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString();

            var paymentId = await _paymentService.ProcessPaymentAsync(
                loanId: message.LoanId,
                clientId: message.ClientId,
                transactionReference: message.PmecReference,
                paymentMethod: "PMEC",
                paymentSource: "Payroll",
                amount: message.Amount,
                transactionDate: message.DeductionDate,
                externalReference: message.PmecReference,
                notes: $"PMEC Payroll Deduction - Period: {message.PayrollPeriod:yyyy-MM}, Employee: {message.EmployeeNumber}",
                createdBy: "System",
                correlationId: correlationId,
                cancellationToken: context.CancellationToken);

            _logger.LogInformation(
                "Successfully processed PMEC payment {PaymentId} for loan {LoanId}",
                paymentId, message.LoanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process PMEC payment for loan {LoanId}, reference {Reference}",
                message.LoanId, message.PmecReference);
            
            throw; // Retry via MassTransit
        }
    }
}
