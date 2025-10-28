using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Events;
using IntelliFin.TreasuryService.Models;
using IntelliFin.TreasuryService.Services;
using Microsoft.Extensions.Logging;

namespace IntelliFin.TreasuryService.Services;

/// <summary>
/// Handles loan disbursement requested events from Loan Origination
/// </summary>
public class LoanDisbursementEventHandler : IDomainEventHandler<LoanDisbursementRequestedEvent>
{
    private readonly ILoanDisbursementService _disbursementService;
    private readonly ITreasuryService _treasuryService;
    private readonly ILogger<LoanDisbursementEventHandler> _logger;

    public LoanDisbursementEventHandler(
        ILoanDisbursementService disbursementService,
        ITreasuryService treasuryService,
        ILogger<LoanDisbursementEventHandler> logger)
    {
        _disbursementService = disbursementService;
        _treasuryService = treasuryService;
        _logger = logger;
    }

    public async Task HandleAsync(LoanDisbursementRequestedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing LoanDisbursementRequestedEvent: DisbursementId={DisbursementId}, LoanId={LoanId}, Amount={Amount}, CorrelationId={CorrelationId}",
            domainEvent.DisbursementId,
            domainEvent.LoanId,
            domainEvent.Amount,
            domainEvent.CorrelationId);

        try
        {
            // 1. Check for duplicate processing using idempotency key
            var existingDisbursement = await _disbursementService.GetByDisbursementIdAsync(domainEvent.DisbursementId);
            if (existingDisbursement != null)
            {
                _logger.LogWarning(
                    "Disbursement already exists - skipping duplicate processing: DisbursementId={DisbursementId}",
                    domainEvent.DisbursementId);
                return;
            }

            // 2. Create disbursement record in database
            var disbursement = new LoanDisbursement
            {
                DisbursementId = domainEvent.DisbursementId,
                LoanId = domainEvent.LoanId,
                ClientId = domainEvent.ClientId,
                Amount = domainEvent.Amount,
                Currency = domainEvent.Currency,
                BankAccountNumber = domainEvent.BankAccountNumber,
                BankCode = domainEvent.BankCode,
                Status = "Received",
                RequestedAt = domainEvent.RequestedAt,
                RequestedBy = domainEvent.RequestedBy,
                CorrelationId = domainEvent.CorrelationId
            };

            var createdDisbursement = await _disbursementService.CreateAsync(disbursement);

            // 3. Create treasury transaction record
            var treasuryTransaction = await _treasuryService.CreateTransactionAsync(
                "Disbursement",
                domainEvent.Amount,
                domainEvent.Currency,
                domainEvent.CorrelationId);

            // 4. Add approval tracking (first approval level)
            await _disbursementService.AddApprovalAsync(
                domainEvent.DisbursementId,
                domainEvent.RequestedBy,
                "System",
                1,
                "Received",
                $"Disbursement request received from Loan Origination. LoanId: {domainEvent.LoanId}, Client: {domainEvent.ClientName}");

            // 5. Log the event processing
            _logger.LogInformation(
                "LoanDisbursementRequestedEvent processed successfully: DisbursementId={DisbursementId}, TreasuryTransactionId={TreasuryTransactionId}",
                domainEvent.DisbursementId,
                treasuryTransaction.TransactionId);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing LoanDisbursementRequestedEvent: DisbursementId={DisbursementId}",
                domainEvent.DisbursementId);

            // Update disbursement status to failed
            try
            {
                await _disbursementService.UpdateStatusAsync(domainEvent.DisbursementId, "Failed", "System");
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update disbursement status after processing error");
            }

            throw; // Re-throw to trigger retry mechanism
        }
    }
}

