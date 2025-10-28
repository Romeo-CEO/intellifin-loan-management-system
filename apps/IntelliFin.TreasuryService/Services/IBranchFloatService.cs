using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Services;

public interface IBranchFloatService
{
    Task<BranchFloat> GetByIdAsync(int id);
    Task<BranchFloat> GetByBranchIdAsync(string branchId);
    Task<IEnumerable<BranchFloat>> GetAllActiveAsync();
    Task<BranchFloat> CreateAsync(BranchFloat branchFloat);
    Task UpdateAsync(BranchFloat branchFloat);
    Task UpdateBalanceAsync(string branchId, decimal newBalance, string transactionType, string correlationId);
}

