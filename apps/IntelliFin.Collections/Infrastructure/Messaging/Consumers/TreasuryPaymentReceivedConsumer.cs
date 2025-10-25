using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Infrastructure.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes Treasury payment events (bank transfers, cash) and processes them.
/// </summary>
public class TreasuryPaymentReceivedConsumer : IConsumer<TreasuryPaymentReceived>
{
    private readonly IPaymentProcessingService _paymentService;
    private readonly ILogger<TreasuryPaymentReceivedConsumer> _logger;

    public TreasuryPaymentReceivedConsumer(
        IPaymentProcessingService paymentService,
        ILogger<TreasuryPaymentReceivedConsumer> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TreasuryPaymentReceived> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Received Treasury payment for loan {LoanId}, amount {Amount}, method {Method}",
            message.LoanId, message.Amount, message.PaymentMethod);

        try
        {
            var correlationId = message.CorrelationId ?? context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString();

            var paymentId = await _paymentService.ProcessPaymentAsync(
                loanId: message.LoanId,
                clientId: message.ClientId,
                transactionReference: message.TransactionReference,
                paymentMethod: message.PaymentMethod,
                paymentSource: GetPaymentSource(message.PaymentMethod),
                amount: message.Amount,
                transactionDate: message.TransactionDate,
                externalReference: message.BankReference,
                notes: message.Notes,
                createdBy: message.ReceivedBy,
                correlationId: correlationId,
                cancellationToken: context.CancellationToken);

            _logger.LogInformation(
                "Successfully processed Treasury payment {PaymentId} for loan {LoanId}",
                paymentId, message.LoanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process Treasury payment for loan {LoanId}, reference {Reference}",
                message.LoanId, message.TransactionReference);
            
            throw; // Retry via MassTransit
        }
    }

    private static string GetPaymentSource(string paymentMethod) => paymentMethod switch
    {
        "BankTransfer" => "BankTransfer",
        "Cash" => "Cash",
        "MobileMoney" => "MobileMoney",
        _ => "Other"
    };
}
