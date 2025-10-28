using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.TreasuryService.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IReconciliationRepository _repository;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        IReconciliationRepository repository,
        ILogger<ReconciliationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ReconciliationBatch> CreateBatchAsync(string batchType, string fileName, int totalEntries)
    {
        _logger.LogInformation("Creating reconciliation batch: Type={BatchType}, FileName={FileName}, Entries={TotalEntries}",
            batchType, fileName, totalEntries);

        var batch = new ReconciliationBatch
        {
            BatchType = batchType,
            FileName = fileName,
            TotalEntries = totalEntries,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow
        };

        return await _repository.CreateBatchAsync(batch);
    }

    public async Task ProcessBankStatementAsync(int batchId)
    {
        _logger.LogInformation("Processing bank statement batch: {BatchId}", batchId);
        // Implementation would parse and process bank statement entries
        // For now, just mark as completed
        var batch = await _repository.GetBatchByIdAsync(batchId);
        if (batch != null)
        {
            batch.Status = "Completed";
            batch.CompletedAt = DateTime.UtcNow;
            await _repository.UpdateBatchAsync(batch);
        }
    }

    public async Task<ReconciliationBatch> GetBatchByIdAsync(int id)
    {
        return await _repository.GetBatchByIdAsync(id);
    }

    public async Task<IEnumerable<ReconciliationEntry>> GetUnmatchedEntriesAsync(int batchId)
    {
        var batch = await _repository.GetBatchByIdAsync(batchId);
        if (batch == null)
        {
            return new List<ReconciliationEntry>();
        }

        return batch.Entries.Where(e => e.MatchStatus == "Unmatched");
    }

    public async Task MatchEntryAsync(int entryId, Guid transactionId, string matchMethod, decimal confidence)
    {
        _logger.LogInformation("Matching reconciliation entry: EntryId={EntryId}, TransactionId={TransactionId}, Method={MatchMethod}",
            entryId, transactionId, matchMethod);

        var entry = await _repository.GetEntryByIdAsync(entryId);
        if (entry != null)
        {
            entry.MatchStatus = "Matched";
            entry.MatchedTransactionId = transactionId;
            entry.MatchMethod = matchMethod;
            entry.MatchConfidence = confidence;
            await _repository.UpdateEntryAsync(entry);
        }
    }
}

