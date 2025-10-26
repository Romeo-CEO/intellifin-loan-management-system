using IntelliFin.Collections.Application.DTOs;
using IntelliFin.Collections.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Application.Services;

public class CollectionsReportingService : ICollectionsReportingService
{
    private readonly CollectionsDbContext _dbContext;
    private readonly ILogger<CollectionsReportingService> _logger;

    public CollectionsReportingService(
        CollectionsDbContext dbContext,
        ILogger<CollectionsReportingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AgingAnalysisReport> GetAgingAnalysisAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating aging analysis report for {AsOfDate}", asOfDate);

        var schedules = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .ToListAsync(cancellationToken);

        var agingBuckets = new List<(string Name, int Min, int? Max)>
        {
            ("Current", 0, 0),
            ("1-30 Days", 1, 30),
            ("31-60 Days", 31, 60),
            ("61-90 Days", 61, 90),
            ("91-180 Days", 91, 180),
            ("181-365 Days", 181, 365),
            ("365+ Days", 365, null)
        };

        var bucketResults = new List<AgingBucket>();
        decimal totalOutstanding = 0;

        foreach (var (name, min, max) in agingBuckets)
        {
            var loansInBucket = schedules.Where(s =>
            {
                var maxDpd = s.Installments
                    .Where(i => i.Status != "Paid")
                    .Select(i => i.DaysPastDue)
                    .DefaultIfEmpty(0)
                    .Max();

                return max.HasValue
                    ? maxDpd >= min && maxDpd <= max.Value
                    : maxDpd >= min;
            }).ToList();

            var outstanding = loansInBucket.Sum(s =>
                s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

            totalOutstanding += outstanding;

            bucketResults.Add(new AgingBucket
            {
                BucketName = name,
                MinDays = min,
                MaxDays = max,
                LoanCount = loansInBucket.Count,
                OutstandingAmount = outstanding,
                PercentageOfTotal = 0 // Will calculate after total is known
            });
        }

        // Calculate percentages
        bucketResults = bucketResults.Select(b => b with
        {
            PercentageOfTotal = totalOutstanding > 0
                ? Math.Round((b.OutstandingAmount / totalOutstanding) * 100, 2)
                : 0
        }).ToList();

        return new AgingAnalysisReport
        {
            AsOfDate = asOfDate,
            AgingBuckets = bucketResults,
            TotalOutstanding = totalOutstanding,
            TotalLoans = schedules.Count
        };
    }

    public async Task<PortfolioAtRiskReport> GetPortfolioAtRiskAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating Portfolio at Risk report for {AsOfDate}", asOfDate);

        var schedules = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .ToListAsync(cancellationToken);

        var totalPortfolio = schedules.Sum(s =>
            s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        var par30 = schedules
            .Where(s => s.Installments.Any(i => i.DaysPastDue >= 30))
            .Sum(s => s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        var par60 = schedules
            .Where(s => s.Installments.Any(i => i.DaysPastDue >= 60))
            .Sum(s => s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        var par90 = schedules
            .Where(s => s.Installments.Any(i => i.DaysPastDue >= 90))
            .Sum(s => s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        var loansInArrears = schedules.Count(s =>
            s.Installments.Any(i => i.DaysPastDue > 0));

        return new PortfolioAtRiskReport
        {
            AsOfDate = asOfDate,
            TotalPortfolioBalance = totalPortfolio,
            Par30Amount = par30,
            Par30Rate = totalPortfolio > 0 ? Math.Round((par30 / totalPortfolio) * 100, 2) : 0,
            Par60Amount = par60,
            Par60Rate = totalPortfolio > 0 ? Math.Round((par60 / totalPortfolio) * 100, 2) : 0,
            Par90Amount = par90,
            Par90Rate = totalPortfolio > 0 ? Math.Round((par90 / totalPortfolio) * 100, 2) : 0,
            TotalLoans = schedules.Count,
            LoansInArrears = loansInArrears
        };
    }

    public async Task<ProvisioningReport> GetProvisioningReportAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating provisioning report for {AsOfDate}", asOfDate);

        var latestClassifications = await _dbContext.ArrearsClassificationHistory
            .GroupBy(h => h.LoanId)
            .Select(g => g.OrderByDescending(h => h.ClassifiedAt).FirstOrDefault())
            .ToListAsync(cancellationToken);

        var schedules = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .ToListAsync(cancellationToken);

        var byClassification = latestClassifications
            .Where(c => c != null)
            .GroupBy(c => c!.NewClassification)
            .Select(g =>
            {
                var classification = g.Key;
                var loanIds = g.Select(c => c!.LoanId).ToHashSet();
                var outstanding = schedules
                    .Where(s => loanIds.Contains(s.LoanId))
                    .Sum(s => s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

                var provisionRate = GetProvisionRate(classification);
                var provisionAmount = outstanding * provisionRate;

                return new ProvisioningByClassification
                {
                    Classification = classification,
                    LoanCount = g.Count(),
                    OutstandingBalance = outstanding,
                    ProvisionRate = provisionRate,
                    ProvisionAmount = provisionAmount
                };
            })
            .OrderBy(p => p.Classification)
            .ToList();

        var totalOutstanding = byClassification.Sum(p => p.OutstandingBalance);
        var totalProvision = byClassification.Sum(p => p.ProvisionAmount);

        return new ProvisioningReport
        {
            AsOfDate = asOfDate,
            ByClassification = byClassification,
            TotalOutstanding = totalOutstanding,
            TotalProvisionRequired = totalProvision,
            ProvisionCoverageRatio = totalOutstanding > 0
                ? Math.Round((totalProvision / totalOutstanding) * 100, 2)
                : 0
        };
    }

    public async Task<RecoveryAnalyticsReport> GetRecoveryAnalyticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating recovery analytics report from {StartDate} to {EndDate}",
            startDate, endDate);

        var payments = await _dbContext.PaymentTransactions
            .Where(p => p.TransactionDate >= startDate && p.TransactionDate <= endDate)
            .Where(p => p.Status == "Confirmed" || p.Status == "Reconciled")
            .ToListAsync(cancellationToken);

        var totalCollected = payments.Sum(p => p.Amount);
        var principalCollected = payments.Sum(p => p.PrincipalPortion);
        var interestCollected = payments.Sum(p => p.InterestPortion);

        var byMethod = payments
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new CollectionsByMethod
            {
                PaymentMethod = g.Key,
                PaymentCount = g.Count(),
                TotalAmount = g.Sum(p => p.Amount),
                PercentageOfTotal = totalCollected > 0
                    ? Math.Round((g.Sum(p => p.Amount) / totalCollected) * 100, 2)
                    : 0
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        return new RecoveryAnalyticsReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalCollected = totalCollected,
            PrincipalCollected = principalCollected,
            InterestCollected = interestCollected,
            PaymentsReceived = payments.Count,
            AveragePaymentSize = payments.Count > 0
                ? Math.Round(totalCollected / payments.Count, 2)
                : 0,
            ByPaymentMethod = byMethod,
            RecoveryRate = 0 // TODO: Calculate against expected collections
        };
    }

    public async Task<CollectionsDashboard> GetCollectionsDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating collections dashboard");

        var schedules = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .ToListAsync(cancellationToken);

        var totalOutstanding = schedules.Sum(s =>
            s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        var totalInArrears = schedules
            .Where(s => s.Installments.Any(i => i.DaysPastDue > 0))
            .Sum(s => s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        var par30 = schedules
            .Where(s => s.Installments.Any(i => i.DaysPastDue >= 30))
            .Sum(s => s.Installments.Sum(i => i.TotalDue - i.TotalPaid));

        // Collections MTD
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var mtdCollections = await _dbContext.PaymentTransactions
            .Where(p => p.TransactionDate >= startOfMonth)
            .Where(p => p.Status == "Confirmed" || p.Status == "Reconciled")
            .SumAsync(p => p.Amount, cancellationToken);

        // Classification breakdown
        var latestClassifications = await _dbContext.ArrearsClassificationHistory
            .GroupBy(h => h.LoanId)
            .Select(g => g.OrderByDescending(h => h.ClassifiedAt).FirstOrDefault())
            .Where(c => c != null)
            .GroupBy(c => c!.NewClassification)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        // Top delinquent loans (placeholder - would need client info)
        var topDelinquent = schedules
            .Where(s => s.Installments.Any(i => i.DaysPastDue > 0))
            .OrderByDescending(s => s.Installments.Max(i => i.DaysPastDue))
            .Take(10)
            .Select(s => new DelinquentLoanSummary
            {
                LoanId = s.LoanId,
                ClientId = s.ClientId,
                ClientName = "Client Name", // TODO: Fetch from Client Management
                OutstandingBalance = s.Installments.Sum(i => i.TotalDue - i.TotalPaid),
                DaysPastDue = s.Installments.Max(i => i.DaysPastDue),
                Classification = "TBD" // TODO: Fetch latest classification
            })
            .ToList();

        return new CollectionsDashboard
        {
            GeneratedAt = DateTime.UtcNow,
            TotalActiveLoans = schedules.Count,
            TotalOutstanding = totalOutstanding,
            TotalInArrears = totalInArrears,
            ArrearsRate = totalOutstanding > 0
                ? Math.Round((totalInArrears / totalOutstanding) * 100, 2)
                : 0,
            CollectionsThisMonth = mtdCollections,
            CollectionsTarget = 0, // TODO: Fetch from targets
            CollectionsAchievement = 0,
            Par30Rate = totalOutstanding > 0
                ? Math.Round((par30 / totalOutstanding) * 100, 2)
                : 0,
            ProvisionCoverageRatio = 0, // TODO: Calculate
            LoansByClassification = latestClassifications,
            TopDelinquentLoans = topDelinquent
        };
    }

    private static decimal GetProvisionRate(string classification) => classification switch
    {
        "Current" => 0.00m,
        "SpecialMention" => 0.00m,
        "Substandard" => 0.20m,
        "Doubtful" => 0.50m,
        "Loss" => 1.00m,
        _ => 0.00m
    };
}
