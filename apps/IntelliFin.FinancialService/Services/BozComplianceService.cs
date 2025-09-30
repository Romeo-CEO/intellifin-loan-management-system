using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.FinancialService.Services;

public class BozComplianceService : IBozComplianceService
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<BozComplianceService> _logger;

    public BozComplianceService(
        LmsDbContext dbContext,
        ILogger<BozComplianceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ComplianceRuleResult> CheckCapitalAdequacyRatioAsync(string branchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking capital adequacy ratio for branch {BranchId}", branchId);

        try
        {
            // Get tier 1 capital (core capital)
            var tier1Capital = await CalculateTier1CapitalAsync(branchId, cancellationToken);
            
            // Get risk-weighted assets
            var riskWeightedAssets = await CalculateRiskWeightedAssetsAsync(branchId, cancellationToken);

            if (riskWeightedAssets == 0)
            {
                return new ComplianceRuleResult
                {
                    RuleId = "BOZ_CAR_001",
                    RuleName = "Capital Adequacy Ratio",
                    Category = ComplianceRuleCategory.CapitalAdequacy,
                    Status = ComplianceStatus.Unknown,
                    Message = "Unable to calculate ratio - no risk-weighted assets found",
                    Severity = ComplianceSeverity.Medium
                };
            }

            // Calculate Capital Adequacy Ratio (CAR)
            var capitalAdequacyRatio = ((double)tier1Capital / (double)riskWeightedAssets) * 100;

            // BoZ minimum requirement is 12%
            const double minimumCarRequirement = 12.0;
            const double warningThreshold = 15.0; // 3% buffer above minimum

            var status = capitalAdequacyRatio >= minimumCarRequirement ? 
                (capitalAdequacyRatio >= warningThreshold ? ComplianceStatus.Compliant : ComplianceStatus.Warning) : 
                ComplianceStatus.NonCompliant;

            var severity = capitalAdequacyRatio < minimumCarRequirement ? ComplianceSeverity.Critical :
                          capitalAdequacyRatio < warningThreshold ? ComplianceSeverity.Medium : ComplianceSeverity.Low;

            return new ComplianceRuleResult
            {
                RuleId = "BOZ_CAR_001",
                RuleName = "Capital Adequacy Ratio",
                Category = ComplianceRuleCategory.CapitalAdequacy,
                Status = status,
                Message = $"Capital Adequacy Ratio: {capitalAdequacyRatio:F2}% (Minimum: {minimumCarRequirement}%)",
                ActualValue = capitalAdequacyRatio,
                Threshold = minimumCarRequirement,
                Severity = severity,
                Metrics = new Dictionary<string, object>
                {
                    ["tier1_capital"] = (double)tier1Capital,
                    ["risk_weighted_assets"] = (double)riskWeightedAssets,
                    ["car_percentage"] = capitalAdequacyRatio,
                    ["minimum_requirement"] = minimumCarRequirement
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating capital adequacy ratio for branch {BranchId}", branchId);
            
            return new ComplianceRuleResult
            {
                RuleId = "BOZ_CAR_001",
                RuleName = "Capital Adequacy Ratio",
                Category = ComplianceRuleCategory.CapitalAdequacy,
                Status = ComplianceStatus.Unknown,
                Message = "Error calculating capital adequacy ratio",
                Severity = ComplianceSeverity.High
            };
        }
    }

    public async Task<ComplianceRuleResult> CheckLoanClassificationComplianceAsync(string branchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking loan classification compliance for branch {BranchId}", branchId);

        try
        {
            // Get all active loans (since we don't have BranchId in LoanApplication, we'll get all active loans)
            // In a real implementation, you would join with a branch relationship or filter differently
            var loans = await _dbContext.LoanApplications
                .Where(l => l.Status == "Approved") // Using Approved as active status
                .Include(l => l.Client)
                .ToListAsync(cancellationToken);

            if (!loans.Any())
            {
                return new ComplianceRuleResult
                {
                    RuleId = "BOZ_LC_001",
                    RuleName = "Loan Classification",
                    Category = ComplianceRuleCategory.LoanClassification,
                    Status = ComplianceStatus.Compliant,
                    Message = "No active loans to classify",
                    Severity = ComplianceSeverity.Low
                };
            }

            var totalLoans = loans.Count;
            var misclassifiedLoans = 0;
            var issues = new List<string>();

            foreach (var loan in loans)
            {
                // For demonstration, assume all loans are classified as "Normal"
                // In a real implementation, you would have a proper risk classification system
                var daysPastDue = 0; // Placeholder since we don't have payment tracking
                
                // Determine expected classification based on BoZ guidelines
                var expectedClassification = GetExpectedLoanClassification(daysPastDue);
                var currentClassification = "Normal"; // Default classification

                if (!string.Equals(currentClassification, expectedClassification, StringComparison.OrdinalIgnoreCase))
                {
                    misclassifiedLoans++;
                    issues.Add($"Loan {loan.Id}: Expected '{expectedClassification}', Current '{currentClassification}' (Days Past Due: {daysPastDue})");
                }
            }

            var compliancePercentage = ((double)(totalLoans - misclassifiedLoans) / totalLoans) * 100;
            const double acceptableThreshold = 95.0; // 95% of loans should be correctly classified

            var status = compliancePercentage >= acceptableThreshold ? ComplianceStatus.Compliant : 
                        compliancePercentage >= 90.0 ? ComplianceStatus.Warning : ComplianceStatus.NonCompliant;

            var severity = misclassifiedLoans == 0 ? ComplianceSeverity.Low :
                          compliancePercentage < 90.0 ? ComplianceSeverity.High : ComplianceSeverity.Medium;

            return new ComplianceRuleResult
            {
                RuleId = "BOZ_LC_001",
                RuleName = "Loan Classification",
                Category = ComplianceRuleCategory.LoanClassification,
                Status = status,
                Message = $"{misclassifiedLoans} of {totalLoans} loans may be misclassified ({compliancePercentage:F1}% compliance)",
                ActualValue = compliancePercentage,
                Threshold = acceptableThreshold,
                Severity = severity,
                Metrics = new Dictionary<string, object>
                {
                    ["total_loans"] = totalLoans,
                    ["misclassified_loans"] = misclassifiedLoans,
                    ["compliance_percentage"] = compliancePercentage,
                    ["issues"] = issues.Take(5).ToList() // Limit to first 5 issues
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking loan classification compliance for branch {BranchId}", branchId);
            
            return new ComplianceRuleResult
            {
                RuleId = "BOZ_LC_001",
                RuleName = "Loan Classification",
                Category = ComplianceRuleCategory.LoanClassification,
                Status = ComplianceStatus.Unknown,
                Message = "Error checking loan classification compliance",
                Severity = ComplianceSeverity.High
            };
        }
    }

    public async Task<ComplianceRuleResult> CheckProvisionCoverageAsync(string branchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking provision coverage for branch {BranchId}", branchId);

        try
        {
            var currentDate = DateTime.UtcNow;
            var provisionData = new Dictionary<string, object>();
            double totalRequiredProvisions = 0;
            double totalActualProvisions = 0;

            // Get loan classifications and calculate required provisions
            var loansByClassification = await _dbContext.LoanApplications
                .Where(l => l.Status == "Approved") // Using Approved as active status
                .GroupBy(l => "Normal") // Default classification since RiskGrade doesn't exist
                .Select(g => new
                {
                    Classification = g.Key,
                    TotalAmount = g.Sum(l => l.Amount), // Using Amount instead of PrincipalAmount
                    Count = g.Count()
                })
                .ToListAsync(cancellationToken);

            foreach (var group in loansByClassification)
            {
                var provisionRate = GetProvisionRate(group.Classification);
                var requiredProvision = (double)group.TotalAmount * provisionRate;
                totalRequiredProvisions += requiredProvision;

                provisionData[$"{group.Classification.ToLower()}_loans_amount"] = group.TotalAmount;
                provisionData[$"{group.Classification.ToLower()}_required_provision"] = requiredProvision;
                provisionData[$"{group.Classification.ToLower()}_provision_rate"] = provisionRate * 100;
            }

            // Get actual provisions from GL accounts (assuming provision accounts exist)
            var actualProvisions = await GetActualProvisionsAsync(branchId, cancellationToken);
            totalActualProvisions = (double)actualProvisions;

            var coverageRatio = totalRequiredProvisions > 0 ? (totalActualProvisions / totalRequiredProvisions) * 100 : 100;
            const double minimumCoverageRequirement = 100.0; // Should be 100% covered

            var status = coverageRatio >= minimumCoverageRequirement ? ComplianceStatus.Compliant :
                        coverageRatio >= 90.0 ? ComplianceStatus.Warning : ComplianceStatus.NonCompliant;

            var severity = coverageRatio < 80.0 ? ComplianceSeverity.Critical :
                          coverageRatio < 90.0 ? ComplianceSeverity.High :
                          coverageRatio < 100.0 ? ComplianceSeverity.Medium : ComplianceSeverity.Low;

            provisionData["total_required_provisions"] = totalRequiredProvisions;
            provisionData["total_actual_provisions"] = totalActualProvisions;
            provisionData["coverage_ratio"] = coverageRatio;

            return new ComplianceRuleResult
            {
                RuleId = "BOZ_PC_001",
                RuleName = "Provision Coverage",
                Category = ComplianceRuleCategory.Provisioning,
                Status = status,
                Message = $"Provision coverage: {coverageRatio:F1}% (Required: {minimumCoverageRequirement}%)",
                ActualValue = coverageRatio,
                Threshold = minimumCoverageRequirement,
                Severity = severity,
                Metrics = provisionData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking provision coverage for branch {BranchId}", branchId);
            
            return new ComplianceRuleResult
            {
                RuleId = "BOZ_PC_001",
                RuleName = "Provision Coverage",
                Category = ComplianceRuleCategory.Provisioning,
                Status = ComplianceStatus.Unknown,
                Message = "Error checking provision coverage",
                Severity = ComplianceSeverity.High
            };
        }
    }

    public async Task<ComplianceRuleResult> CheckLargeExposureLimitsAsync(string branchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking large exposure limits for branch {BranchId}", branchId);

        try
        {
            // Get tier 1 capital for the branch
            var tier1Capital = await CalculateTier1CapitalAsync(branchId, cancellationToken);

            if (tier1Capital <= 0)
            {
                return new ComplianceRuleResult
                {
                    RuleId = "BOZ_LE_001",
                    RuleName = "Large Exposure Limits",
                    Category = ComplianceRuleCategory.LargeExposures,
                    Status = ComplianceStatus.Unknown,
                    Message = "Cannot calculate large exposure limits - tier 1 capital is zero or negative",
                    Severity = ComplianceSeverity.High
                };
            }

            // BoZ limits: 25% of tier 1 capital per individual, 800% aggregate
            const double individualLimit = 0.25; // 25%
            const double aggregateLimit = 8.0; // 800%

            var individualLimitAmount = (double)tier1Capital * individualLimit;
            var aggregateLimitAmount = (double)tier1Capital * aggregateLimit;

            // Get exposures by client (individual or group)
            var clientExposures = await _dbContext.LoanApplications
                .Where(l => l.Status == "Approved")
                .Include(l => l.Client)
                .GroupBy(l => l.Client!.NationalId) // Group by client NRC
                .Select(g => new
                {
                    ClientId = g.Key,
                    TotalExposure = g.Sum(l => l.Amount) // Using full amount since we don't track payments here
                })
                .Where(e => e.TotalExposure > 0)
                .ToListAsync(cancellationToken);

            var largeExposures = clientExposures
                .Where(e => (double)e.TotalExposure > individualLimitAmount)
                .ToList();

            var totalLargeExposures = largeExposures.Sum(e => (double)e.TotalExposure);
            var violations = largeExposures.Count;

            var individualComplianceStatus = violations == 0 ? ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;
            var aggregateComplianceStatus = totalLargeExposures <= aggregateLimitAmount ? 
                ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;

            var overallStatus = individualComplianceStatus == ComplianceStatus.Compliant && 
                               aggregateComplianceStatus == ComplianceStatus.Compliant ? 
                               ComplianceStatus.Compliant : ComplianceStatus.NonCompliant;

            var severity = violations > 0 || totalLargeExposures > aggregateLimitAmount ? 
                          ComplianceSeverity.Critical : ComplianceSeverity.Low;

            return new ComplianceRuleResult
            {
                RuleId = "BOZ_LE_001",
                RuleName = "Large Exposure Limits",
                Category = ComplianceRuleCategory.LargeExposures,
                Status = overallStatus,
                Message = $"{violations} individual limit violations, aggregate exposure: {totalLargeExposures / (double)tier1Capital * 100:F1}% of tier 1 capital",
                ActualValue = violations,
                Threshold = 0,
                Severity = severity,
                Metrics = new Dictionary<string, object>
                {
                    ["tier1_capital"] = (double)tier1Capital,
                    ["individual_limit_amount"] = individualLimitAmount,
                    ["aggregate_limit_amount"] = aggregateLimitAmount,
                    ["individual_violations"] = violations,
                    ["total_large_exposures"] = totalLargeExposures,
                    ["aggregate_exposure_ratio"] = totalLargeExposures / (double)tier1Capital * 100,
                    ["violating_clients"] = largeExposures.Take(5).Select(e => new { e.ClientId, Exposure = e.TotalExposure }).ToList()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking large exposure limits for branch {BranchId}", branchId);
            
            return new ComplianceRuleResult
            {
                RuleId = "BOZ_LE_001",
                RuleName = "Large Exposure Limits",
                Category = ComplianceRuleCategory.LargeExposures,
                Status = ComplianceStatus.Unknown,
                Message = "Error checking large exposure limits",
                Severity = ComplianceSeverity.High
            };
        }
    }

    public async Task<ComplianceRuleResult> CheckReportingDeadlinesAsync(string branchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking regulatory reporting deadlines for branch {BranchId}", branchId);

        try
        {
            var currentDate = DateTime.UtcNow;
            var currentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
            var previousMonth = currentMonth.AddMonths(-1);

            var upcomingDeadlines = new List<object>();
            var overdueReports = new List<object>();

            // BoZ reporting requirements
            var reportingRequirements = new[]
            {
                new { Name = "Prudential Return", Frequency = "Monthly", DeadlineDay = 15, IsCritical = true },
                new { Name = "NPL Classification", Frequency = "Monthly", DeadlineDay = 10, IsCritical = true },
                new { Name = "Capital Adequacy", Frequency = "Quarterly", DeadlineDay = 20, IsCritical = true },
                new { Name = "Large Exposures", Frequency = "Monthly", DeadlineDay = 12, IsCritical = false }
            };

            foreach (var requirement in reportingRequirements)
            {
                DateTime deadlineDate;
                
                if (requirement.Frequency == "Monthly")
                {
                    deadlineDate = new DateTime(currentDate.Year, currentDate.Month, requirement.DeadlineDay);
                    if (deadlineDate < currentDate)
                    {
                        deadlineDate = deadlineDate.AddMonths(1);
                    }
                }
                else // Quarterly
                {
                    var quarter = ((currentDate.Month - 1) / 3) + 1;
                    var quarterStartMonth = (quarter - 1) * 3 + 1;
                    var nextQuarterMonth = quarterStartMonth + 3;
                    if (nextQuarterMonth > 12)
                    {
                        nextQuarterMonth -= 12;
                        deadlineDate = new DateTime(currentDate.Year + 1, nextQuarterMonth, requirement.DeadlineDay);
                    }
                    else
                    {
                        deadlineDate = new DateTime(currentDate.Year, nextQuarterMonth, requirement.DeadlineDay);
                    }
                }

                var daysUntilDeadline = (deadlineDate - currentDate).Days;

                if (daysUntilDeadline < 0)
                {
                    overdueReports.Add(new
                    {
                        Name = requirement.Name,
                        DeadlineDate = deadlineDate,
                        DaysOverdue = Math.Abs(daysUntilDeadline),
                        IsCritical = requirement.IsCritical
                    });
                }
                else if (daysUntilDeadline <= 7)
                {
                    upcomingDeadlines.Add(new
                    {
                        Name = requirement.Name,
                        DeadlineDate = deadlineDate,
                        DaysRemaining = daysUntilDeadline,
                        IsCritical = requirement.IsCritical
                    });
                }
            }

            var criticalOverdue = overdueReports.Cast<dynamic>().Count(r => r.IsCritical);
            var totalOverdue = overdueReports.Count;

            var status = totalOverdue == 0 ? ComplianceStatus.Compliant :
                        criticalOverdue == 0 ? ComplianceStatus.Warning : ComplianceStatus.NonCompliant;

            var severity = criticalOverdue > 0 ? ComplianceSeverity.Critical :
                          totalOverdue > 0 ? ComplianceSeverity.High : 
                          upcomingDeadlines.Count > 0 ? ComplianceSeverity.Medium : ComplianceSeverity.Low;

            return new ComplianceRuleResult
            {
                RuleId = "BOZ_RD_001",
                RuleName = "Regulatory Reporting Deadlines",
                Category = ComplianceRuleCategory.RegulatoryReporting,
                Status = status,
                Message = $"{totalOverdue} overdue reports ({criticalOverdue} critical), {upcomingDeadlines.Count} upcoming deadlines",
                ActualValue = totalOverdue,
                Threshold = 0,
                Severity = severity,
                Metrics = new Dictionary<string, object>
                {
                    ["overdue_reports"] = overdueReports,
                    ["upcoming_deadlines"] = upcomingDeadlines,
                    ["critical_overdue"] = criticalOverdue,
                    ["total_overdue"] = totalOverdue
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking reporting deadlines for branch {BranchId}", branchId);
            
            return new ComplianceRuleResult
            {
                RuleId = "BOZ_RD_001",
                RuleName = "Regulatory Reporting Deadlines",
                Category = ComplianceRuleCategory.RegulatoryReporting,
                Status = ComplianceStatus.Unknown,
                Message = "Error checking reporting deadlines",
                Severity = ComplianceSeverity.High
            };
        }
    }

    public async Task<ComplianceRuleResult> CheckLiquidityRatiosAsync(string branchId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking liquidity ratios for branch {BranchId}", branchId);

        try
        {
            // Calculate liquid assets (cash, bank balances, short-term investments)
            var liquidAssets = await GetLiquidAssetsAsync(branchId, cancellationToken);
            
            // Calculate total deposits
            var totalDeposits = await GetTotalDepositsAsync(branchId, cancellationToken);

            if (totalDeposits <= 0)
            {
                return new ComplianceRuleResult
                {
                    RuleId = "BOZ_LR_001",
                    RuleName = "Liquidity Ratio",
                    Category = ComplianceRuleCategory.LiquidityRatio,
                    Status = ComplianceStatus.Unknown,
                    Message = "Cannot calculate liquidity ratio - no deposits found",
                    Severity = ComplianceSeverity.Medium
                };
            }

            var liquidityRatio = ((double)liquidAssets / (double)totalDeposits) * 100;
            
            // BoZ minimum liquidity ratio requirement (typically 20% for microfinance)
            const double minimumLiquidityRatio = 20.0;
            const double warningThreshold = 25.0; // 5% buffer above minimum

            var status = liquidityRatio >= minimumLiquidityRatio ?
                (liquidityRatio >= warningThreshold ? ComplianceStatus.Compliant : ComplianceStatus.Warning) :
                ComplianceStatus.NonCompliant;

            var severity = liquidityRatio < minimumLiquidityRatio ? ComplianceSeverity.Critical :
                          liquidityRatio < warningThreshold ? ComplianceSeverity.Medium : ComplianceSeverity.Low;

            return new ComplianceRuleResult
            {
                RuleId = "BOZ_LR_001",
                RuleName = "Liquidity Ratio",
                Category = ComplianceRuleCategory.LiquidityRatio,
                Status = status,
                Message = $"Liquidity ratio: {liquidityRatio:F2}% (Minimum: {minimumLiquidityRatio}%)",
                ActualValue = liquidityRatio,
                Threshold = minimumLiquidityRatio,
                Severity = severity,
                Metrics = new Dictionary<string, object>
                {
                    ["liquid_assets"] = liquidAssets,
                    ["total_deposits"] = totalDeposits,
                    ["liquidity_ratio"] = liquidityRatio,
                    ["minimum_requirement"] = minimumLiquidityRatio
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking liquidity ratios for branch {BranchId}", branchId);
            
            return new ComplianceRuleResult
            {
                RuleId = "BOZ_LR_001",
                RuleName = "Liquidity Ratio",
                Category = ComplianceRuleCategory.LiquidityRatio,
                Status = ComplianceStatus.Unknown,
                Message = "Error checking liquidity ratios",
                Severity = ComplianceSeverity.High
            };
        }
    }

    #region Private Helper Methods

    private async Task<decimal> CalculateTier1CapitalAsync(string branchId, CancellationToken cancellationToken)
    {
        // This would typically query GL accounts for capital components
        // For now, returning a placeholder calculation
        var capitalAccounts = await _dbContext.GLAccounts
            .Where(a => a.AccountType.Contains("Capital") || a.AccountType.Contains("Equity"))
            .SumAsync(a => a.CurrentBalance, cancellationToken);

        return capitalAccounts;
    }

    private async Task<decimal> CalculateRiskWeightedAssetsAsync(string branchId, CancellationToken cancellationToken)
    {
        // Simplified calculation - would normally apply risk weights based on asset types
        var totalLoans = await _dbContext.LoanApplications
            .Where(l => l.Status == "Approved")
            .SumAsync(l => l.Amount, cancellationToken);

        // Apply standard risk weight (typically 100% for commercial loans)
        return totalLoans;
    }

    private int CalculateDaysPastDue(IntelliFin.Shared.DomainModels.Entities.LoanApplication loan)
    {
        // This would calculate based on payment schedule and last payment
        // For now, returning a placeholder since we don't have payment tracking
        return 0;
    }

    private string GetExpectedLoanClassification(int daysPastDue)
    {
        // BoZ loan classification guidelines
        return daysPastDue switch
        {
            <= 30 => "Normal",
            <= 90 => "Watch",
            <= 180 => "Substandard",
            <= 365 => "Doubtful",
            _ => "Loss"
        };
    }

    private double GetProvisionRate(string classification)
    {
        // BoZ provision rates
        return classification.ToUpper() switch
        {
            "NORMAL" => 0.01,      // 1%
            "WATCH" => 0.03,       // 3%
            "SUBSTANDARD" => 0.20, // 20%
            "DOUBTFUL" => 0.50,    // 50%
            "LOSS" => 1.00,        // 100%
            _ => 0.01              // Default to 1%
        };
    }

    private async Task<decimal> GetActualProvisionsAsync(string branchId, CancellationToken cancellationToken)
    {
        // Get provision balances from GL accounts
        var provisionAccounts = await _dbContext.GLAccounts
            .Where(a => a.AccountType.Contains("Provision"))
            .SumAsync(a => a.CurrentBalance, cancellationToken);

        return Math.Abs(provisionAccounts); // Provisions are typically negative balances
    }

    private async Task<decimal> GetLiquidAssetsAsync(string branchId, CancellationToken cancellationToken)
    {
        // Calculate liquid assets from GL accounts
        var liquidAssetAccounts = await _dbContext.GLAccounts
            .Where(a => a.AccountType.Contains("Cash") || 
                       a.AccountType.Contains("Bank") ||
                       a.AccountType.Contains("Short-term Investment"))
            .SumAsync(a => a.CurrentBalance, cancellationToken);

        return liquidAssetAccounts;
    }

    private async Task<decimal> GetTotalDepositsAsync(string branchId, CancellationToken cancellationToken)
    {
        // Calculate total deposits from GL accounts
        var depositAccounts = await _dbContext.GLAccounts
            .Where(a => a.AccountType.Contains("Deposit"))
            .SumAsync(a => Math.Abs(a.CurrentBalance), cancellationToken);

        return depositAccounts;
    }

    #endregion
}