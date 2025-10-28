using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.TreasuryService.Services;

public class BranchFloatService : IBranchFloatService
{
    private readonly IBranchFloatRepository _repository;
    private readonly ILogger<BranchFloatService> _logger;

    public BranchFloatService(
        IBranchFloatRepository repository,
        ILogger<BranchFloatService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BranchFloat> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<BranchFloat> GetByBranchIdAsync(string branchId)
    {
        return await _repository.GetByBranchIdAsync(branchId);
    }

    public async Task<IEnumerable<BranchFloat>> GetAllActiveAsync()
    {
        return await _repository.GetAllActiveAsync();
    }

    public async Task<BranchFloat> CreateAsync(BranchFloat branchFloat)
    {
        _logger.LogInformation("Creating branch float for {BranchId}", branchFloat.BranchId);
        return await _repository.CreateAsync(branchFloat);
    }

    public async Task UpdateAsync(BranchFloat branchFloat)
    {
        _logger.LogInformation("Updating branch float for {BranchId}", branchFloat.BranchId);
        await _repository.UpdateAsync(branchFloat);
    }

    public async Task UpdateBalanceAsync(string branchId, decimal newBalance, string transactionType, string correlationId)
    {
        _logger.LogInformation("Updating branch float balance: BranchId={BranchId}, NewBalance={NewBalance}, TransactionType={TransactionType}",
            branchId, newBalance, transactionType);

        var branchFloat = await _repository.GetByBranchIdAsync(branchId);
        if (branchFloat == null)
        {
            throw new KeyNotFoundException($"Branch float not found for branch {branchId}");
        }

        branchFloat.CurrentBalance = newBalance;
        branchFloat.LastUpdated = DateTime.UtcNow;
        branchFloat.LastUpdatedBy = correlationId;

        await _repository.UpdateAsync(branchFloat);

        _logger.LogInformation("Branch float balance updated successfully: BranchId={BranchId}", branchId);
    }
}

