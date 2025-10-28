using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Data;
using IntelliFin.TreasuryService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.TreasuryService.Infrastructure;

public class BranchFloatRepository : BaseRepository<BranchFloat>, IBranchFloatRepository
{
    public BranchFloatRepository(TreasuryDbContext context) : base(context)
    {
    }

    public async Task<BranchFloat> GetByBranchIdAsync(string branchId)
    {
        return await _context.BranchFloats
            .FirstOrDefaultAsync(bf => bf.BranchId == branchId);
    }

    public async Task<IEnumerable<BranchFloat>> GetAllActiveAsync()
    {
        return await _context.BranchFloats
            .Where(bf => bf.Status == "Active")
            .OrderBy(bf => bf.BranchName)
            .ToListAsync();
    }

    public async Task<IEnumerable<BranchFloat>> GetByStatusAsync(string status)
    {
        return await _context.BranchFloats
            .Where(bf => bf.Status == status)
            .OrderBy(bf => bf.BranchName)
            .ToListAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var branchFloat = await _context.BranchFloats.FindAsync(id);
        if (branchFloat != null)
        {
            _context.BranchFloats.Remove(branchFloat);
            await _context.SaveChangesAsync();
        }
    }
}
