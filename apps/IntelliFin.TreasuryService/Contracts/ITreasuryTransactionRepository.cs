using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Contracts;

public interface ITreasuryTransactionRepository
{
    Task<TreasuryTransaction> GetByIdAsync(int id);
    Task<TreasuryTransaction> GetByTransactionIdAsync(Guid transactionId);
    Task<IEnumerable<TreasuryTransaction>> GetByCorrelationIdAsync(string correlationId);
    Task<IEnumerable<TreasuryTransaction>> GetByStatusAsync(string status);
    Task<IEnumerable<TreasuryTransaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<TreasuryTransaction> CreateAsync(TreasuryTransaction transaction);
    Task UpdateAsync(TreasuryTransaction transaction);
    Task DeleteAsync(int id);
}

