using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Services;

public interface IReconciliationService
{
    Task<ReconciliationBatch> CreateBatchAsync(string batchType, string fileName, int totalEntries);
    Task ProcessBankStatementAsync(int batchId);
    Task<ReconciliationBatch> GetBatchByIdAsync(int id);
    Task<IEnumerable<ReconciliationEntry>> GetUnmatchedEntriesAsync(int batchId);
    Task MatchEntryAsync(int entryId, Guid transactionId, string matchMethod, decimal confidence);
}

