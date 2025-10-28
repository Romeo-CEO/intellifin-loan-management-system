using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Contracts;

public interface IBranchFloatRepository
{
    Task<BranchFloat> GetByIdAsync(int id);
    Task<BranchFloat> GetByBranchIdAsync(string branchId);
    Task<IEnumerable<BranchFloat>> GetAllActiveAsync();
    Task<IEnumerable<BranchFloat>> GetByStatusAsync(string status);
    Task<BranchFloat> CreateAsync(BranchFloat branchFloat);
    Task UpdateAsync(BranchFloat branchFloat);
    Task DeleteAsync(int id);
}

