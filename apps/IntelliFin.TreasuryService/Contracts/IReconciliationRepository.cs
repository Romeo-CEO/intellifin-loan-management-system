using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Contracts;

public interface IReconciliationRepository
{
    Task<ReconciliationBatch> GetBatchByIdAsync(int id);
    Task<ReconciliationBatch> GetBatchByBatchIdAsync(Guid batchId);
    Task<IEnumerable<ReconciliationBatch>> GetBatchesByStatusAsync(string status);
    Task<IEnumerable<ReconciliationBatch>> GetBatchesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<ReconciliationBatch> CreateBatchAsync(ReconciliationBatch batch);
    Task UpdateBatchAsync(ReconciliationBatch batch);

    Task<ReconciliationEntry> GetEntryByIdAsync(int id);
    Task<IEnumerable<ReconciliationEntry>> GetEntriesByBatchIdAsync(int batchId);
    Task<IEnumerable<ReconciliationEntry>> GetEntriesByMatchStatusAsync(string matchStatus);
    Task<ReconciliationEntry> CreateEntryAsync(ReconciliationEntry entry);
    Task UpdateEntryAsync(ReconciliationEntry entry);
    Task DeleteEntryAsync(int id);
}

