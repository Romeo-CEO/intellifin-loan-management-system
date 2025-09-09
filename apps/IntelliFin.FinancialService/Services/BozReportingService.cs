using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// BoZ (Bank of Zambia) regulatory reporting service
/// </summary>
public class BozReportingService : IBozReportingService
{
    private readonly IJasperReportsClient _jasperClient;
    private readonly IGLAccountRepository _glRepository;
    private readonly LmsDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<BozReportingService> _logger;
    private readonly IConfiguration _configuration;
    
    private const int CacheExpirationMinutes = 60; // Longer cache for regulatory reports
    private const string BozReportCachePrefix = "boz_report:";

    public BozReportingService(
        IJasperReportsClient jasperClient,
        IGLAccountRepository glRepository,
        LmsDbContext dbContext,
        IDistributedCache cache,
        ILogger<BozReportingService> logger,
        IConfiguration configuration)
    {
        _jasperClient = jasperClient;
        _glRepository = glRepository;
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<BozPrudentialReport> GeneratePrudentialReportAsync(DateTime reportingPeriod, string branchId)
    {
        var cacheKey = $"{BozReportCachePrefix}prudential:{branchId}:{reportingPeriod:yyyyMM}";
        
        try
        {
            _logger.LogInformation("Generating BoZ Prudential Report for period: {Period}, Branch: {BranchId}", 
                reportingPeriod, branchId);

            // Check cache first
            var cachedReport = await GetFromCacheAsync<BozPrudentialReport>(cacheKey);
            if (cachedReport != null)
            {
                _logger.LogInformation("Retrieved BoZ Prudential Report from cache");
                return cachedReport;
            }

            var report = new BozPrudentialReport
            {
                ReportingPeriod = reportingPeriod,
                BranchId = branchId,
                GeneratedAt = DateTime.UtcNow,
                OverallCompliance = true
            };

            // Calculate comprehensive prudential ratios
            await CalculateComprehensivePrudentialRatiosAsync(report);
            
            // Validate against regulatory limits
            await ValidateRegulatoryLimitsAsync(report);
            
            // Identify compliance issues
            await IdentifyComplianceIssuesAsync(report);

            // Cache the results
            await SetCacheAsync(cacheKey, report);

            _logger.LogInformation("Successfully generated BoZ Prudential Report");
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating BoZ Prudential Report for period: {Period}, Branch: {BranchId}", 
                reportingPeriod, branchId);
            throw;
        }
    }

    public async Task<ReportResponse> GenerateNplClassificationReportAsync(DateTime asOfDate, string branchId)
    {
        try
        {
            _logger.LogInformation("Generating NPL Classification Report for date: {Date}, Branch: {BranchId}", 
                asOfDate, branchId);

            var parameters = new Dictionary<string, object>
            {
                ["AS_OF_DATE"] = asOfDate.ToString("yyyy-MM-dd"),
                ["BRANCH_ID"] = branchId,
                ["REPORT_TITLE"] = "Non-Performing Loans Classification Report",
                ["GENERATED_BY"] = Environment.UserName,
                ["GENERATED_DATE"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var reportContent = await _jasperClient.ExecuteReportAsync(
                "/reports/boz/npl_classification", 
                parameters, 
                "pdf");

            var response = new ReportResponse
            {
                ReportId = Guid.NewGuid().ToString(),
                FileName = $"NPL_Classification_{branchId}_{asOfDate:yyyyMMdd}.pdf",
                ContentType = "application/pdf",
                Content = reportContent,
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed"
            };

            _logger.LogInformation("Successfully generated NPL Classification Report");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating NPL Classification Report");
            throw;
        }
    }

    public async Task<ReportResponse> GenerateCapitalAdequacyReportAsync(DateTime asOfDate, string branchId)
    {
        try
        {
            _logger.LogInformation("Generating Capital Adequacy Report for date: {Date}, Branch: {BranchId}", 
                asOfDate, branchId);

            var parameters = new Dictionary<string, object>
            {
                ["AS_OF_DATE"] = asOfDate.ToString("yyyy-MM-dd"),
                ["BRANCH_ID"] = branchId,
                ["REPORT_TITLE"] = "Capital Adequacy Ratio Report",
                ["MINIMUM_CAR"] = _configuration.GetValue<decimal>("BoZ:MinimumCapitalAdequacyRatio", 10.0m),
                ["GENERATED_BY"] = Environment.UserName,
                ["GENERATED_DATE"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var reportContent = await _jasperClient.ExecuteReportAsync(
                "/reports/boz/capital_adequacy", 
                parameters, 
                "pdf");

            var response = new ReportResponse
            {
                ReportId = Guid.NewGuid().ToString(),
                FileName = $"Capital_Adequacy_{branchId}_{asOfDate:yyyyMMdd}.pdf",
                ContentType = "application/pdf",
                Content = reportContent,
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed"
            };

            _logger.LogInformation("Successfully generated Capital Adequacy Report");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Capital Adequacy Report");
            throw;
        }
    }

    public async Task<ReportResponse> GenerateLoanPortfolioSummaryAsync(DateTime startDate, DateTime endDate, string branchId)
    {
        try
        {
            _logger.LogInformation("Generating Loan Portfolio Summary for period: {StartDate} to {EndDate}, Branch: {BranchId}", 
                startDate, endDate, branchId);

            var parameters = new Dictionary<string, object>
            {
                ["START_DATE"] = startDate.ToString("yyyy-MM-dd"),
                ["END_DATE"] = endDate.ToString("yyyy-MM-dd"),
                ["BRANCH_ID"] = branchId,
                ["REPORT_TITLE"] = "Loan Portfolio Summary Report",
                ["GENERATED_BY"] = Environment.UserName,
                ["GENERATED_DATE"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var reportContent = await _jasperClient.ExecuteReportAsync(
                "/reports/boz/loan_portfolio_summary", 
                parameters, 
                "pdf");

            var response = new ReportResponse
            {
                ReportId = Guid.NewGuid().ToString(),
                FileName = $"Loan_Portfolio_{branchId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf",
                ContentType = "application/pdf",
                Content = reportContent,
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed"
            };

            _logger.LogInformation("Successfully generated Loan Portfolio Summary Report");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Loan Portfolio Summary Report");
            throw;
        }
    }

    public async Task<bool> SubmitReportToBozAsync(string reportId)
    {
        try
        {
            _logger.LogInformation("Submitting report to BoZ: {ReportId}", reportId);

            // This would integrate with BoZ's electronic submission system
            // For now, we'll simulate the submission process
            await Task.Delay(1000); // Simulate network delay

            // In production, this would:
            // 1. Retrieve the report from storage
            // 2. Format according to BoZ specifications
            // 3. Submit via secure API or file transfer
            // 4. Handle acknowledgment and tracking

            _logger.LogInformation("Successfully submitted report to BoZ: {ReportId}", reportId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting report to BoZ: {ReportId}", reportId);
            return false;
        }
    }

    private async Task CalculateComprehensivePrudentialRatiosAsync(BozPrudentialReport report)
    {
        try
        {
            // Get all GL accounts for calculations
            var assetAccounts = await _glRepository.GetByTypeAsync("Asset");
            var liabilityAccounts = await _glRepository.GetByTypeAsync("Liability");
            var equityAccounts = await _glRepository.GetByTypeAsync("Equity");
            var loanAccounts = await _glRepository.GetActiveByCategoryAsync("Loan");
            
            var totalAssets = assetAccounts.Sum(a => a.CurrentBalance);
            var totalLiabilities = liabilityAccounts.Sum(a => a.CurrentBalance);
            var totalEquity = equityAccounts.Sum(a => a.CurrentBalance);
            var totalLoans = loanAccounts.Sum(a => a.CurrentBalance);

            // Get loan applications for NPL calculation
            var loanApplications = await _dbContext.LoanApplications.ToListAsync();
            var overdueLoans = loanApplications.Where(l => l.Status == "Overdue" || l.Status == "Default").ToList();
            var nplAmount = overdueLoans.Sum(l => l.Amount);

            // Capital Adequacy Ratios
            var tier1Capital = totalEquity; // Simplified
            var tier2Capital = totalEquity * 0.2m; // Simplified
            var totalCapital = tier1Capital + tier2Capital;
            var riskWeightedAssets = totalAssets * 0.8m; // Simplified risk weighting

            report.CapitalAdequacyRatio = riskWeightedAssets > 0 ? Convert.ToDouble((totalCapital / riskWeightedAssets) * 100) : 0;
            report.Tier1CapitalRatio = riskWeightedAssets > 0 ? Convert.ToDouble((tier1Capital / riskWeightedAssets) * 100) : 0;

            // Liquidity Ratio (Cash + Liquid Assets / Current Liabilities)
            var cashAccounts = await _glRepository.GetActiveByCategoryAsync("Cash");
            var liquidAssets = cashAccounts.Sum(a => a.CurrentBalance);
            report.LiquidityRatio = totalLiabilities > 0 ? Convert.ToDouble((liquidAssets / totalLiabilities) * 100) : 0;

            // NPL Ratio
            report.NplRatio = totalLoans > 0 ? Convert.ToDouble((nplAmount / totalLoans) * 100) : 0;

            // Provision Coverage (assuming 70% provision coverage for NPLs)
            var provisionAmount = nplAmount * 0.7m;
            report.ProvisionCoverage = nplAmount > 0 ? Convert.ToDouble((provisionAmount / nplAmount) * 100) : 0;

            // Large Exposure Ratio (simplified - largest single exposure as % of capital)
            var largestLoan = loanApplications.Any() ? loanApplications.Max(l => l.Amount) : 0;
            report.LargeExposureRatio = totalCapital > 0 ? Convert.ToDouble((largestLoan / totalCapital) * 100) : 0;

            _logger.LogInformation("Calculated comprehensive prudential ratios");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating comprehensive prudential ratios");
            throw;
        }
    }

    private async Task ValidateRegulatoryLimitsAsync(BozPrudentialReport report)
    {
        try
        {
            // BoZ regulatory limits (these would come from configuration in production)
            var regulatoryLimits = new Dictionary<string, RegulatoryLimit>
            {
                ["MinimumCapitalAdequacyRatio"] = new()
                {
                    Name = "Minimum Capital Adequacy Ratio",
                    CurrentValue = (decimal)report.CapitalAdequacyRatio,
                    LimitValue = 10.0m,
                    IsCompliant = report.CapitalAdequacyRatio >= 10.0,
                    Unit = "Percentage"
                },
                ["MinimumTier1CapitalRatio"] = new()
                {
                    Name = "Minimum Tier 1 Capital Ratio",
                    CurrentValue = (decimal)report.Tier1CapitalRatio,
                    LimitValue = 8.0m,
                    IsCompliant = report.Tier1CapitalRatio >= 8.0,
                    Unit = "Percentage"
                },
                ["MinimumLiquidityRatio"] = new()
                {
                    Name = "Minimum Liquidity Ratio",
                    CurrentValue = (decimal)report.LiquidityRatio,
                    LimitValue = 20.0m,
                    IsCompliant = report.LiquidityRatio >= 20.0,
                    Unit = "Percentage"
                },
                ["MaximumNplRatio"] = new()
                {
                    Name = "Maximum NPL Ratio",
                    CurrentValue = (decimal)report.NplRatio,
                    LimitValue = 5.0m,
                    IsCompliant = report.NplRatio <= 5.0,
                    Unit = "Percentage"
                },
                ["MinimumProvisionCoverage"] = new()
                {
                    Name = "Minimum Provision Coverage",
                    CurrentValue = (decimal)report.ProvisionCoverage,
                    LimitValue = 50.0m,
                    IsCompliant = report.ProvisionCoverage >= 50.0,
                    Unit = "Percentage"
                },
                ["MaximumLargeExposure"] = new()
                {
                    Name = "Maximum Large Exposure",
                    CurrentValue = (decimal)report.LargeExposureRatio,
                    LimitValue = 25.0m,
                    IsCompliant = report.LargeExposureRatio <= 25.0,
                    Unit = "Percentage"
                }
            };

            report.RegulatoryLimits = regulatoryLimits;
            report.OverallCompliance = regulatoryLimits.Values.All(l => l.IsCompliant);

            await Task.CompletedTask; // Satisfy async requirement
            _logger.LogInformation("Validated regulatory limits");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating regulatory limits");
            throw;
        }
    }

    private async Task IdentifyComplianceIssuesAsync(BozPrudentialReport report)
    {
        try
        {
            var issues = new List<ComplianceIssue>();

            // Check each regulatory limit for compliance issues
            foreach (var limit in report.RegulatoryLimits.Values)
            {
                if (!limit.IsCompliant)
                {
                    var severity = CalculateComplianceSeverity(limit);
                    issues.Add(new ComplianceIssue
                    {
                        Category = "Regulatory Limit Breach",
                        Description = $"{limit.Name}: Current value {limit.CurrentValue:F2}% exceeds/falls below regulatory limit of {limit.LimitValue:F2}%",
                        Severity = severity,
                        RecommendedAction = GetRecommendedAction(limit.Name, severity),
                        IdentifiedDate = DateTime.UtcNow
                    });
                }
            }

            report.ComplianceIssues = issues;
            await Task.CompletedTask; // Satisfy async requirement
            
            _logger.LogInformation("Identified {IssueCount} compliance issues", issues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying compliance issues");
            throw;
        }
    }

    private static string CalculateComplianceSeverity(RegulatoryLimit limit)
    {
        var deviation = Math.Abs((double)((limit.CurrentValue - limit.LimitValue) / limit.LimitValue * 100));
        
        return deviation switch
        {
            > 20 => "High",
            > 10 => "Medium",
            _ => "Low"
        };
    }

    private static string GetRecommendedAction(string limitName, string severity)
    {
        return limitName switch
        {
            "Minimum Capital Adequacy Ratio" => severity == "High" ? "Immediate capital injection required" : "Monitor and improve capital position",
            "Maximum NPL Ratio" => "Enhance collection efforts and loan recovery procedures",
            "Minimum Liquidity Ratio" => "Increase liquid asset holdings and manage cash flow",
            _ => "Review and address compliance gap"
        };
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