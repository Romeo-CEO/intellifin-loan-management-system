using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Services;

public interface ITreasuryService
{
    Task<TreasuryTransaction> GetTransactionByIdAsync(int id);
    Task<TreasuryTransaction> GetTransactionByTransactionIdAsync(Guid transactionId);
    Task<IEnumerable<TreasuryTransaction>> GetTransactionsByCorrelationIdAsync(string correlationId);
    Task<TreasuryTransaction> CreateTransactionAsync(string transactionType, decimal amount, string currency, string? correlationId = null);
    Task UpdateTransactionStatusAsync(Guid transactionId, string status, string? errorMessage = null);
    Task ProcessDisbursementAsync(Guid disbursementId, decimal amount, string bankAccount, string bankCode, string? correlationId);
}
