using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Events;
using MassTransit;

namespace IntelliFin.TreasuryService.Consumers;

/// <summary>
/// MassTransit consumer for loan disbursement requested events from Loan Origination
/// </summary>
public class LoanDisbursementConsumer : IConsumer<LoanDisbursementRequestedEvent>
{
    private readonly IDomainEventHandler<LoanDisbursementRequestedEvent> _eventHandler;
    private readonly ILogger<LoanDisbursementConsumer> _logger;

    public LoanDisbursementConsumer(
        IDomainEventHandler<LoanDisbursementRequestedEvent> eventHandler,
        ILogger<LoanDisbursementConsumer> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LoanDisbursementRequestedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Consuming LoanDisbursementRequestedEvent: DisbursementId={DisbursementId}, LoanId={LoanId}, Amount={Amount}, MessageId={MessageId}, CorrelationId={CorrelationId}",
            evt.DisbursementId,
            evt.LoanId,
            evt.Amount,
            context.MessageId,
            evt.CorrelationId);

        try
        {
            await _eventHandler.HandleAsync(evt, context.CancellationToken);

            _logger.LogInformation(
                "LoanDisbursementRequestedEvent processed successfully: DisbursementId={DisbursementId}",
                evt.DisbursementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing LoanDisbursementRequestedEvent: DisbursementId={DisbursementId}",
                evt.DisbursementId);

            // Re-throw to trigger MassTransit retry mechanism
            throw;
        }
    }
}

