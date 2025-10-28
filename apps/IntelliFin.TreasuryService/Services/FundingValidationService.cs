using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.TreasuryService.Services;

/// <summary>
/// Service for validating funding sources for disbursements
/// </summary>
public class FundingValidationService : IFundingValidationService
{
    private readonly IBranchFloatRepository _branchFloatRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<FundingValidationService> _logger;

    // Cache keys for balance information
    private const string BranchFloatCacheKey = "branch_float_{0}";
    private const int CacheExpirationMinutes = 5;

    public FundingValidationService(
        IBranchFloatRepository branchFloatRepository,
        IDistributedCache cache,
        ILogger<FundingValidationService> logger)
    {
        _branchFloatRepository = branchFloatRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Validates if sufficient funds are available for disbursement
    /// </summary>
    public async Task<FundingValidationResult> ValidateFundingAsync(string branchId, decimal requiredAmount, string preferredSource = "BranchFloat")
    {
        _logger.LogInformation(
            "Validating funding for disbursement: BranchId={BranchId}, Amount={RequiredAmount}, PreferredSource={PreferredSource}",
            branchId, requiredAmount, preferredSource);

        var result = new FundingValidationResult
        {
            DisbursementAmount = requiredAmount,
            BranchId = branchId,
            PreferredSource = preferredSource,
            ValidationTime = DateTime.UtcNow
        };

        try
        {
            // First, try the preferred funding source
            if (preferredSource == "BranchFloat")
            {
                var branchFloatResult = await ValidateBranchFloatAsync(branchId, requiredAmount);
                if (branchFloatResult.IsValid)
                {
                    result.SelectedSource = "BranchFloat";
                    result.AvailableAmount = branchFloatResult.AvailableAmount;
                    result.IsValid = true;
                    _logger.LogInformation(
                        "Branch float funding validated: BranchId={BranchId}, Available={AvailableAmount}",
                        branchId, branchFloatResult.AvailableAmount);
                    return result;
                }
            }

            // If branch float insufficient, check central account
            var centralAccountResult = await ValidateCentralAccountAsync(requiredAmount);
            if (centralAccountResult.IsValid)
            {
                result.SelectedSource = "CentralAccount";
                result.AvailableAmount = centralAccountResult.AvailableAmount;
                result.IsValid = true;
                _logger.LogInformation(
                    "Central account funding validated: Available={AvailableAmount}",
                    centralAccountResult.AvailableAmount);
                return result;
            }

            // Insufficient funds in both sources
            result.IsValid = false;
            result.ErrorMessage = "Insufficient funds in both branch float and central account";
            _logger.LogWarning(
                "Insufficient funds for disbursement: BranchId={BranchId}, Amount={RequiredAmount}",
                branchId, requiredAmount);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating funding for disbursement: BranchId={BranchId}", branchId);
            result.IsValid = false;
            result.ErrorMessage = $"Funding validation failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Validates branch float funding source
    /// </summary>
    private async Task<FundingSourceResult> ValidateBranchFloatAsync(string branchId, decimal requiredAmount)
    {
        var cacheKey = string.Format(BranchFloatCacheKey, branchId);

        // Try to get from cache first
        var cachedBalance = await _cache.GetStringAsync(cacheKey);
        decimal currentBalance;

        if (!string.IsNullOrEmpty(cachedBalance))
        {
            currentBalance = JsonSerializer.Deserialize<decimal>(cachedBalance);
            _logger.LogDebug("Retrieved branch float balance from cache: BranchId={BranchId}, Balance={CurrentBalance}", branchId, currentBalance);
        }
        else
        {
            // Get from database
            var branchFloat = await _branchFloatRepository.GetByBranchIdAsync(branchId);
            if (branchFloat == null)
            {
                return new FundingSourceResult
                {
                    IsValid = false,
                    ErrorMessage = $"Branch float not found for branch {branchId}"
                };
            }

            currentBalance = branchFloat.CurrentBalance;

            // Cache the balance
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(currentBalance), cacheOptions);
            _logger.LogDebug("Cached branch float balance: BranchId={BranchId}, Balance={CurrentBalance}", branchId, currentBalance);
        }

        // Check if sufficient funds are available
        var availableAmount = currentBalance;
        var isValid = availableAmount >= requiredAmount;

        return new FundingSourceResult
        {
            IsValid = isValid,
            AvailableAmount = availableAmount,
            RequiredAmount = requiredAmount,
            SourceType = "BranchFloat",
            ErrorMessage = isValid ? null : $"Insufficient branch float balance. Available: {availableAmount}, Required: {requiredAmount}"
        };
    }

    /// <summary>
    /// Validates central account funding source
    /// </summary>
    private async Task<FundingSourceResult> ValidateCentralAccountAsync(decimal requiredAmount)
    {
        // TODO: Implement central account balance checking
        // For now, assume central account has sufficient funds
        const decimal CentralAccountBalance = 1000000; // 1M MWK

        var isValid = CentralAccountBalance >= requiredAmount;

        return new FundingSourceResult
        {
            IsValid = isValid,
            AvailableAmount = CentralAccountBalance,
            RequiredAmount = requiredAmount,
            SourceType = "CentralAccount",
            ErrorMessage = isValid ? null : $"Insufficient central account balance. Available: {CentralAccountBalance}, Required: {requiredAmount}"
        };
    }

    /// <summary>
    /// Updates branch float balance after successful disbursement
    /// </summary>
    public async Task UpdateBranchFloatBalanceAsync(string branchId, decimal disbursementAmount, string correlationId)
    {
        _logger.LogInformation(
            "Updating branch float balance: BranchId={BranchId}, DisbursementAmount={DisbursementAmount}",
            branchId, disbursementAmount);

        var branchFloat = await _branchFloatRepository.GetByBranchIdAsync(branchId);
        if (branchFloat == null)
        {
            _logger.LogWarning("Branch float not found for balance update: BranchId={BranchId}", branchId);
            return;
        }

        var newBalance = branchFloat.CurrentBalance - disbursementAmount;
        branchFloat.CurrentBalance = newBalance;
        branchFloat.LastUpdated = DateTime.UtcNow;
        branchFloat.LastUpdatedBy = correlationId;

        await _branchFloatRepository.UpdateAsync(branchFloat);

        // Update cache
        var cacheKey = string.Format(BranchFloatCacheKey, branchId);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(newBalance), cacheOptions);

        _logger.LogInformation(
            "Branch float balance updated: BranchId={BranchId}, OldBalance={OldBalance}, NewBalance={NewBalance}",
            branchId, branchFloat.CurrentBalance + disbursementAmount, newBalance);
    }
}

/// <summary>
/// Result of funding source validation
/// </summary>
public class FundingValidationResult
{
    public bool IsValid { get; set; }
    public decimal DisbursementAmount { get; set; }
    public string BranchId { get; set; } = string.Empty;
    public string PreferredSource { get; set; } = string.Empty;
    public string SelectedSource { get; set; } = string.Empty;
    public decimal AvailableAmount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of individual funding source validation
/// </summary>
public class FundingSourceResult
{
    public bool IsValid { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal RequiredAmount { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Interface for funding validation service
/// </summary>
public interface IFundingValidationService
{
    Task<FundingValidationResult> ValidateFundingAsync(string branchId, decimal requiredAmount, string preferredSource = "BranchFloat");
    Task UpdateBranchFloatBalanceAsync(string branchId, decimal disbursementAmount, string correlationId);
}

