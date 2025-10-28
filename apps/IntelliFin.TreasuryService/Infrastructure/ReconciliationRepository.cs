using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Data;
using IntelliFin.TreasuryService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.TreasuryService.Infrastructure;

public class ReconciliationRepository : IReconciliationRepository
{
    private readonly TreasuryDbContext _context;

    public ReconciliationRepository(TreasuryDbContext context)
    {
        _context = context;
    }

    // Batch operations
    public async Task<ReconciliationBatch> GetBatchByIdAsync(int id)
    {
        return await _context.ReconciliationBatches
            .Include(b => b.Entries)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<ReconciliationBatch> GetBatchByBatchIdAsync(Guid batchId)
    {
        return await _context.ReconciliationBatches
            .Include(b => b.Entries)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);
    }

    public async Task<IEnumerable<ReconciliationBatch>> GetBatchesByStatusAsync(string status)
    {
        return await _context.ReconciliationBatches
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReconciliationBatch>> GetBatchesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.ReconciliationBatches
            .Where(b => b.CreatedAt >= startDate && b.CreatedAt <= endDate)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<ReconciliationBatch> CreateBatchAsync(ReconciliationBatch batch)
    {
        _context.ReconciliationBatches.Add(batch);
        await _context.SaveChangesAsync();
        return batch;
    }

    public async Task UpdateBatchAsync(ReconciliationBatch batch)
    {
        _context.ReconciliationBatches.Update(batch);
        await _context.SaveChangesAsync();
    }

    // Entry operations
    public async Task<ReconciliationEntry> GetEntryByIdAsync(int id)
    {
        return await _context.ReconciliationEntries
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<ReconciliationEntry>> GetEntriesByBatchIdAsync(int batchId)
    {
        return await _context.ReconciliationEntries
            .Where(e => e.BatchId == batchId)
            .OrderBy(e => e.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReconciliationEntry>> GetEntriesByMatchStatusAsync(string matchStatus)
    {
        return await _context.ReconciliationEntries
            .Where(e => e.MatchStatus == matchStatus)
            .OrderBy(e => e.TransactionDate)
            .ToListAsync();
    }

    public async Task<ReconciliationEntry> CreateEntryAsync(ReconciliationEntry entry)
    {
        _context.ReconciliationEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task UpdateEntryAsync(ReconciliationEntry entry)
    {
        _context.ReconciliationEntries.Update(entry);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(int id)
    {
        var entry = await GetEntryByIdAsync(id);
        if (entry != null)
        {
            _context.ReconciliationEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}

