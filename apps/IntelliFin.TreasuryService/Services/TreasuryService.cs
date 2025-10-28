using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.TreasuryService.Services;

public class TreasuryService : ITreasuryService
{
    private readonly ITreasuryTransactionRepository _transactionRepository;
    private readonly ILogger<TreasuryService> _logger;

    public TreasuryService(
        ITreasuryTransactionRepository transactionRepository,
        ILogger<TreasuryService> logger)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<TreasuryTransaction> GetTransactionByIdAsync(int id)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
        {
            throw new KeyNotFoundException($"Treasury transaction with ID {id} not found");
        }
        return transaction;
    }

    public async Task<TreasuryTransaction> GetTransactionByTransactionIdAsync(Guid transactionId)
    {
        var transaction = await _transactionRepository.GetByTransactionIdAsync(transactionId);
        if (transaction == null)
        {
            throw new KeyNotFoundException($"Treasury transaction with TransactionId {transactionId} not found");
        }
        return transaction;
    }

    public async Task<IEnumerable<TreasuryTransaction>> GetTransactionsByCorrelationIdAsync(string correlationId)
    {
        return await _transactionRepository.GetByCorrelationIdAsync(correlationId);
    }

    public async Task<TreasuryTransaction> CreateTransactionAsync(string transactionType, decimal amount, string currency, string? correlationId = null)
    {
        _logger.LogInformation("Creating treasury transaction: Type={TransactionType}, Amount={Amount}, Currency={Currency}, CorrelationId={CorrelationId}",
            transactionType, amount, currency, correlationId);

        var transaction = new TreasuryTransaction
        {
            TransactionType = transactionType,
            Amount = amount,
            Currency = currency,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdTransaction = await _transactionRepository.CreateAsync(transaction);

        _logger.LogInformation("Treasury transaction created successfully: TransactionId={TransactionId}", createdTransaction.TransactionId);

        return createdTransaction;
    }

    public async Task UpdateTransactionStatusAsync(Guid transactionId, string status, string? errorMessage = null)
    {
        _logger.LogInformation("Updating treasury transaction status: TransactionId={TransactionId}, Status={Status}",
            transactionId, status);

        var transaction = await _transactionRepository.GetByTransactionIdAsync(transactionId);
        if (transaction == null)
        {
            throw new KeyNotFoundException($"Treasury transaction with TransactionId {transactionId} not found");
        }

        transaction.Status = status;
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.ProcessedAt = status == "Completed" || status == "Failed" ? DateTime.UtcNow : null;
        transaction.ErrorMessage = errorMessage;

        await _transactionRepository.UpdateAsync(transaction);

        _logger.LogInformation("Treasury transaction status updated successfully: TransactionId={TransactionId}, Status={Status}",
            transactionId, status);
    }

    public async Task ProcessDisbursementAsync(Guid disbursementId, decimal amount, string bankAccount, string bankCode, string? correlationId)
    {
        _logger.LogInformation("Processing loan disbursement: DisbursementId={DisbursementId}, Amount={Amount}, BankAccount={BankAccount}, CorrelationId={CorrelationId}",
            disbursementId, amount, bankAccount, correlationId);

        // Create disbursement transaction
        var transaction = await CreateTransactionAsync("Disbursement", amount, "MWK", correlationId);

        try
        {
            // Here we would integrate with banking API
            // For now, we'll simulate the processing

            _logger.LogInformation("Simulating bank API call for disbursement: {DisbursementId}", disbursementId);

            // Simulate processing time
            await Task.Delay(100);

            // Update transaction as completed
            await UpdateTransactionStatusAsync(transaction.TransactionId, "Completed");

            _logger.LogInformation("Disbursement processed successfully: DisbursementId={DisbursementId}, TransactionId={TransactionId}",
                disbursementId, transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process disbursement: DisbursementId={DisbursementId}", disbursementId);
            await UpdateTransactionStatusAsync(transaction.TransactionId, "Failed", ex.Message);
            throw;
        }
    }
}
