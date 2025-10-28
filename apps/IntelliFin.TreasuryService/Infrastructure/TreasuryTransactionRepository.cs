using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Data;
using IntelliFin.TreasuryService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.TreasuryService.Infrastructure;

public class TreasuryTransactionRepository : BaseRepository<TreasuryTransaction>, ITreasuryTransactionRepository
{
    public TreasuryTransactionRepository(TreasuryDbContext context) : base(context)
    {
    }

    public async Task<TreasuryTransaction> GetByTransactionIdAsync(Guid transactionId)
    {
        return await _context.TreasuryTransactions
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
    }

    public async Task<IEnumerable<TreasuryTransaction>> GetByCorrelationIdAsync(string correlationId)
    {
        return await _context.TreasuryTransactions
            .Where(t => t.CorrelationId == correlationId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TreasuryTransaction>> GetByStatusAsync(string status)
    {
        return await _context.TreasuryTransactions
            .Where(t => t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TreasuryTransaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.TreasuryTransactions
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var transaction = await _context.TreasuryTransactions.FindAsync(id);
        if (transaction != null)
        {
            _context.TreasuryTransactions.Remove(transaction);
            await _context.SaveChangesAsync();
        }
    }
}
