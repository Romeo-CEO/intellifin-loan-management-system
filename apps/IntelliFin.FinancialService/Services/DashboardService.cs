using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// Service for real-time dashboard data and metrics
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly LmsDbContext _dbContext;
    private readonly IGLAccountRepository _glRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DashboardService> _logger;
    
    private const int CacheExpirationMinutes = 15;
    private const string DashboardCacheKeyPrefix = "dashboard:";
    private const string BalanceCacheKeyPrefix = "balance:";
    private const string LoanMetricsCacheKeyPrefix = "loans:";
    private const string CashFlowCacheKeyPrefix = "cashflow:";
    private const string CollectionCacheKeyPrefix = "collections:";

    public DashboardService(
        LmsDbContext dbContext,
        IGLAccountRepository glRepository,
        IDistributedCache cache,
        ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _glRepository = glRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DashboardMetrics> GetDashboardMetricsAsync(string? branchId = null)
    {
        var cacheKey = $"{DashboardCacheKeyPrefix}{branchId ?? "ALL"}";
        
        try
        {
            _logger.LogInformation("Retrieving dashboard metrics for branch: {BranchId}", branchId ?? "All");

            // Try to get from cache first
            var cachedMetrics = await GetFromCacheAsync<DashboardMetrics>(cacheKey);
            if (cachedMetrics != null)
            {
                _logger.LogInformation("Retrieved dashboard metrics from cache for branch: {BranchId}", branchId ?? "All");
                return cachedMetrics;
            }

            var metrics = new DashboardMetrics
            {
                BranchId = branchId,
                AsOfDate = DateTime.UtcNow
            };

            // Get account balance summaries
            var balances = await GetAccountBalanceSummaryAsync(branchId);
            
            metrics.TotalAssets = balances.Where(kvp => kvp.Key.Contains("Asset")).Sum(kvp => kvp.Value);
            metrics.TotalLiabilities = balances.Where(kvp => kvp.Key.Contains("Liability")).Sum(kvp => kvp.Value);
            metrics.NetWorth = metrics.TotalAssets - metrics.TotalLiabilities;
            metrics.CashPosition = balances.GetValueOrDefault("Cash", 0);

            // Get loan metrics
            var loanMetrics = await GetLoanPerformanceMetricsAsync(branchId);
            metrics.TotalLoanBalance = Convert.ToDecimal(loanMetrics.GetValueOrDefault("TotalOutstanding", 0m));
            metrics.ActiveLoans = Convert.ToInt32(loanMetrics.GetValueOrDefault("ActiveCount", 0));
            metrics.NplRatio = Convert.ToDouble(loanMetrics.GetValueOrDefault("NPLRatio", 0m));
            metrics.AverrageLoanSize = Convert.ToDecimal(loanMetrics.GetValueOrDefault("AverageLoanAmount", 0m));
            metrics.LoanApplicationsPending = Convert.ToInt32(loanMetrics.GetValueOrDefault("PendingApproval", 0));

            // Calculate Portfolio at Risk (PAR)
            var nonPerformingAmount = Convert.ToDecimal(loanMetrics.GetValueOrDefault("NonPerforming", 0m));
            metrics.PortfolioAtRisk = metrics.TotalLoanBalance > 0 ? 
                Convert.ToDouble((nonPerformingAmount / metrics.TotalLoanBalance) * 100) : 0;

            // Get financial performance metrics
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var cashFlowMetrics = await GetCashFlowMetricsAsync(startOfMonth, today, branchId);
            
            metrics.MonthlyInterestIncome = cashFlowMetrics.GetValueOrDefault("InterestIncome", 0);
            metrics.MonthlyOperatingExpenses = cashFlowMetrics.GetValueOrDefault("OperatingExpenses", 0);
            metrics.MonthlyDisbursements = cashFlowMetrics.GetValueOrDefault("LoanDisbursements", 0);
            metrics.MonthlyCollections = cashFlowMetrics.GetValueOrDefault("LoanRepayments", 0);

            // Get collection metrics
            var collectionMetrics = await GetCollectionMetricsAsync(branchId);
            var collectionRate = Convert.ToDecimal(collectionMetrics.GetValueOrDefault("CollectionRate", 70m));
            metrics.ProvisionCoverage = collectionRate; // Simplified mapping

            // Calculate operational metrics
            metrics.NewClientsThisMonth = await GetNewClientsCountAsync(startOfMonth, today, branchId);
            metrics.AverageProcessingTime = CalculateAverageProcessingTime();

            // Add risk ratios (placeholder calculations for BoZ compliance)
            metrics.CapitalAdequacyRatio = CalculateCapitalAdequacyRatio(metrics);

            // Cache the results
            await SetCacheAsync(cacheKey, metrics);

            _logger.LogInformation("Successfully retrieved and cached dashboard metrics for branch: {BranchId}", branchId ?? "All");
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard metrics for branch: {BranchId}", branchId ?? "All");
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetAccountBalanceSummaryAsync(string? branchId = null)
    {
        try
        {
            _logger.LogInformation("Retrieving account balance summary for branch: {BranchId}", branchId ?? "All");

            // Get accounts by different categories since we don't have GetAllAsync
            var assetAccounts = await _glRepository.GetByTypeAsync("Asset");
            var liabilityAccounts = await _glRepository.GetByTypeAsync("Liability");
            var cashAccounts = await _glRepository.GetActiveByCategoryAsync("Cash");
            var loanAccounts = await _glRepository.GetActiveByCategoryAsync("Loan");
            
            // Combine all accounts
            var accounts = assetAccounts.Concat(liabilityAccounts).Concat(cashAccounts).Concat(loanAccounts);

            var balanceSummary = new Dictionary<string, decimal>();

            // Group by account category
            var groupedAccounts = accounts.GroupBy(a => a.Category);

            foreach (var group in groupedAccounts)
            {
                balanceSummary[group.Key] = group.Sum(a => a.CurrentBalance);
            }

            // Add specific account summaries
            balanceSummary["Cash"] = accounts
                .Where(a => a.Name.Contains("Cash", StringComparison.OrdinalIgnoreCase))
                .Sum(a => a.CurrentBalance);

            balanceSummary["Loans"] = accounts
                .Where(a => a.Name.Contains("Loan", StringComparison.OrdinalIgnoreCase))
                .Sum(a => a.CurrentBalance);

            _logger.LogInformation("Retrieved {Count} account categories for balance summary", balanceSummary.Count);
            return balanceSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving account balance summary");
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetLoanPerformanceMetricsAsync(string? branchId = null)
    {
        try
        {
            _logger.LogInformation("Retrieving loan performance metrics for branch: {BranchId}", branchId ?? "All");

            var query = _dbContext.LoanApplications.AsQueryable();

            // Note: LoanApplication doesn't have BranchId in current model
            // Branch filtering would need to be implemented via Client relationship

            var loans = await query.ToListAsync();

            var metrics = new Dictionary<string, object>
            {
                ["TotalCount"] = loans.Count,
                ["ActiveCount"] = loans.Count(l => l.Status == "Active" || l.Status == "Disbursed"),
                ["TotalOutstanding"] = loans.Where(l => l.Status == "Active" || l.Status == "Disbursed")
                                           .Sum(l => l.Amount), // Using Amount instead of LoanAmount
                ["NonPerforming"] = loans.Where(l => l.Status == "Overdue" || l.Status == "Default")
                                         .Sum(l => l.Amount),
                ["NewApplicationsToday"] = loans.Count(l => l.CreatedAtUtc.Date == DateTime.Today),
                ["PendingApproval"] = loans.Count(l => l.Status == "Pending"),
                ["ApprovedToday"] = loans.Count(l => l.ApprovedAt?.Date == DateTime.Today),
                ["AverageLoanAmount"] = loans.Any() ? loans.Average(l => l.Amount) : 0m
            };

            // Calculate performance ratios
            var totalOutstanding = Convert.ToDecimal(metrics["TotalOutstanding"]);
            var nonPerforming = Convert.ToDecimal(metrics["NonPerforming"]);
            
            metrics["NPLRatio"] = totalOutstanding > 0 ? (nonPerforming / totalOutstanding) * 100 : 0m;
            metrics["CollectionEfficiency"] = CalculateCollectionEfficiency(loans);

            _logger.LogInformation("Retrieved loan performance metrics with {TotalLoans} total loans", loans.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan performance metrics");
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetCashFlowMetricsAsync(DateTime startDate, DateTime endDate, string? branchId = null)
    {
        try
        {
            _logger.LogInformation("Retrieving cash flow metrics for period: {StartDate} to {EndDate}, Branch: {BranchId}", 
                startDate, endDate, branchId ?? "All");

            // Use GLEntryLines since GLEntry uses TransactionDate
            var query = _dbContext.GLEntryLines
                .Include(el => el.GLEntry)
                .Include(el => el.GLAccount)
                .Where(el => el.GLEntry.TransactionDate >= startDate && el.GLEntry.TransactionDate <= endDate);

            var entryLines = await query.ToListAsync();

            var metrics = new Dictionary<string, decimal>
            {
                ["TotalInflows"] = entryLines.Where(el => el.DebitAmount > 0 && 
                                                         (el.GLAccount.Category.Contains("Revenue") || 
                                                          el.GLAccount.Category.Contains("Income")))
                                           .Sum(el => el.DebitAmount),
                
                ["TotalOutflows"] = entryLines.Where(el => el.CreditAmount > 0 && 
                                                          (el.GLAccount.Category.Contains("Expense") || 
                                                           el.GLAccount.Category.Contains("Cost")))
                                             .Sum(el => el.CreditAmount),

                ["InterestIncome"] = entryLines.Where(el => el.GLAccount.Name.Contains("Interest", StringComparison.OrdinalIgnoreCase) &&
                                                           el.DebitAmount > 0)
                                              .Sum(el => el.DebitAmount),

                ["OperatingExpenses"] = entryLines.Where(el => el.GLAccount.Category.Contains("Operating Expense"))
                                                 .Sum(el => el.CreditAmount),

                ["LoanDisbursements"] = entryLines.Where(el => el.GLAccount.Name.Contains("Loan", StringComparison.OrdinalIgnoreCase) &&
                                                             el.DebitAmount > 0)
                                                 .Sum(el => el.DebitAmount),

                ["LoanRepayments"] = entryLines.Where(el => el.GLAccount.Name.Contains("Loan", StringComparison.OrdinalIgnoreCase) &&
                                                           el.CreditAmount > 0)
                                              .Sum(el => el.CreditAmount)
            };

            // Calculate net cash flow
            metrics["NetCashFlow"] = metrics["TotalInflows"] - metrics["TotalOutflows"];

            _logger.LogInformation("Retrieved cash flow metrics with {EntryCount} GL entry lines", entryLines.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cash flow metrics");
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetCollectionMetricsAsync(string? branchId = null)
    {
        try
        {
            _logger.LogInformation("Retrieving collection metrics for branch: {BranchId}", branchId ?? "All");

            var query = _dbContext.LoanApplications.AsQueryable();

            // Note: LoanApplication doesn't have BranchId in current model
            // Branch filtering would need to be implemented via Client relationship

            var loans = await query.Where(l => l.Status == "Active" || l.Status == "Disbursed" || l.Status == "Overdue")
                                  .ToListAsync();

            var today = DateTime.Today;
            var metrics = new Dictionary<string, object>();

            // Calculate Days Past Due (DPD) categories - simplified without DueDate
            // Note: LoanApplication model doesn't have DueDate, so this is a placeholder implementation
            var overdueLoans = loans.Where(l => l.Status == "Overdue").Count();
            
            // Simplified DPD categorization - in production would calculate based on actual due dates
            metrics["DPD_0_30"] = loans.Count(l => l.Status == "Active");
            metrics["DPD_31_60"] = Math.Min(overdueLoans / 3, overdueLoans);
            metrics["DPD_61_90"] = Math.Min(overdueLoans / 3, overdueLoans);
            metrics["DPD_90_Plus"] = overdueLoans - (int)metrics["DPD_31_60"] - (int)metrics["DPD_61_90"];

            // Collection efficiency metrics (simplified without AmountPaid)
            var totalAmountDue = loans.Sum(l => l.Amount);
            // Note: AmountPaid is not in the current model, so assuming 70% collection rate
            var estimatedAmountCollected = totalAmountDue * 0.7m; // Placeholder calculation
            
            metrics["CollectionRate"] = totalAmountDue > 0 ? (estimatedAmountCollected / totalAmountDue) * 100 : 0m;
            metrics["OutstandingAmount"] = totalAmountDue - estimatedAmountCollected;
            metrics["OverdueLoans"] = overdueLoans;

            _logger.LogInformation("Retrieved collection metrics for {LoanCount} loans", loans.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving collection metrics");
            throw;
        }
    }

    private static decimal CalculateCollectionEfficiency(List<IntelliFin.Shared.DomainModels.Entities.LoanApplication> loans)
    {
        if (!loans.Any()) return 0m;

        // Simplified calculation since AmountPaid is not in the model
        // In production, this would calculate actual payments vs dues
        var totalDue = loans.Sum(l => l.Amount);
        var estimatedCollected = totalDue * 0.7m; // Assume 70% collection efficiency

        return totalDue > 0 ? (estimatedCollected / totalDue) * 100 : 0m;
    }

    private async Task<int> GetNewClientsCountAsync(DateTime startDate, DateTime endDate, string? branchId)
    {
        try
        {
            // Note: Client entities would need to be added to get actual new client count
            // For now, return a placeholder calculation based on loan applications
            var newApplicationsCount = await _dbContext.LoanApplications
                .Where(la => la.CreatedAtUtc >= startDate && la.CreatedAtUtc <= endDate)
                .CountAsync();

            // Estimate unique clients (assuming 80% are new clients)
            return (int)(newApplicationsCount * 0.8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating new clients count");
            return 0;
        }
    }

    private double CalculateAverageProcessingTime()
    {
        try
        {
            var recentApprovedLoans = _dbContext.LoanApplications
                .Where(la => la.ApprovedAt.HasValue && la.CreatedAtUtc > DateTime.UtcNow.AddDays(-30))
                .ToList();

            if (!recentApprovedLoans.Any()) return 0;

            var averageDays = recentApprovedLoans
                .Where(la => la.ApprovedAt.HasValue)
                .Average(la => (la.ApprovedAt!.Value - la.CreatedAtUtc).TotalDays);

            return Math.Round(averageDays, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average processing time");
            return 0;
        }
    }

    private double CalculateCapitalAdequacyRatio(DashboardMetrics metrics)
    {
        // Simplified BoZ capital adequacy calculation
        // In production, this would use actual capital components and risk-weighted assets
        var estimatedCapital = metrics.NetWorth;
        var riskWeightedAssets = metrics.TotalLoanBalance * 1.2m; // Simplified risk weighting

        return riskWeightedAssets > 0 ? Convert.ToDouble((estimatedCapital / riskWeightedAssets) * 100) : 0;
    }

    #region Cache Helper Methods

    private async Task<T?> GetFromCacheAsync<T>(string cacheKey) where T : class
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedValue))
            {
                return JsonSerializer.Deserialize<T>(cachedValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache with key: {CacheKey}", cacheKey);
        }
        
        return null;
    }

    private async Task SetCacheAsync<T>(string cacheKey, T value) where T : class
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };
            
            await _cache.SetStringAsync(cacheKey, serializedValue, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache with key: {CacheKey}", cacheKey);
        }
    }

    #endregion
}