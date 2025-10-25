using IntelliFin.Collections.Domain.Entities;
using IntelliFin.Collections.Infrastructure.Persistence;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Application.Services;

public class ArrearsClassificationService : IArrearsClassificationService
{
    private readonly CollectionsDbContext _dbContext;
    private readonly IAuditClient _auditClient;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ArrearsClassificationService> _logger;

    // BoZ Classification Thresholds (could be moved to Vault)
    private readonly Dictionary<string, (int MinDpd, int MaxDpd, decimal ProvisionRate)> _bozClassifications = new()
    {
        { "Current", (0, 0, 0.00m) },
        { "SpecialMention", (1, 89, 0.00m) },
        { "Substandard", (90, 179, 0.20m) },
        { "Doubtful", (180, 364, 0.50m) },
        { "Loss", (365, int.MaxValue, 1.00m) }
    };

    public ArrearsClassificationService(
        CollectionsDbContext dbContext,
        IAuditClient auditClient,
        INotificationService notificationService,
        ILogger<ArrearsClassificationService> logger)
    {
        _dbContext = dbContext;
        _auditClient = auditClient;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<int> ClassifyAllLoansAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting nightly arrears classification for all active loans");

        var schedules = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .ToListAsync(cancellationToken);

        var classifiedCount = 0;

        foreach (var schedule in schedules)
        {
            try
            {
                await ClassifyLoanInternalAsync(schedule, cancellationToken);
                classifiedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to classify loan {LoanId}", schedule.LoanId);
            }
        }

        _logger.LogInformation(
            "Completed nightly arrears classification. Classified {Count} loans",
            classifiedCount);

        return classifiedCount;
    }

    public async Task ClassifyLoanAsync(
        Guid loanId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.LoanId == loanId, cancellationToken);

        if (schedule == null)
        {
            throw new InvalidOperationException($"No repayment schedule found for loan {loanId}");
        }

        await ClassifyLoanInternalAsync(schedule, cancellationToken);
    }

    private async Task ClassifyLoanInternalAsync(
        RepaymentSchedule schedule,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Calculate DPD for each installment
        var maxDpd = 0;
        foreach (var installment in schedule.Installments)
        {
            if (installment.Status != "Paid")
            {
                var dpd = installment.DueDate < today
                    ? (today - installment.DueDate.Date).Days
                    : 0;

                installment.DaysPastDue = dpd;
                installment.UpdatedAtUtc = DateTime.UtcNow;

                // Update status based on DPD
                if (dpd > 0 && installment.Status == "Pending")
                {
                    installment.Status = "Overdue";
                }

                maxDpd = Math.Max(maxDpd, dpd);
            }
        }

        // Determine classification based on max DPD
        var newClassification = DetermineClassification(maxDpd);
        
        // Get previous classification
        var previousClassification = await _dbContext.ArrearsClassificationHistory
            .Where(h => h.LoanId == schedule.LoanId)
            .OrderByDescending(h => h.ClassifiedAt)
            .Select(h => h.NewClassification)
            .FirstOrDefaultAsync(cancellationToken) ?? "Current";

        // Only create history record if classification changed
        if (newClassification != previousClassification)
        {
            var outstandingBalance = schedule.Installments.Sum(i => i.TotalDue - i.TotalPaid);
            var provisionRate = GetProvisionRate(newClassification);
            var provisionAmount = outstandingBalance * provisionRate;
            var isNonAccrual = newClassification is "Substandard" or "Doubtful" or "Loss";

            var classificationHistory = new ArrearsClassificationHistory
            {
                Id = Guid.NewGuid(),
                LoanId = schedule.LoanId,
                PreviousClassification = previousClassification,
                NewClassification = newClassification,
                DaysPastDue = maxDpd,
                OutstandingBalance = outstandingBalance,
                ProvisionRate = provisionRate,
                ProvisionAmount = provisionAmount,
                IsNonAccrual = isNonAccrual,
                ClassifiedAt = DateTime.UtcNow,
                ClassifiedBy = "System",
                Reason = $"BoZ Classification - {maxDpd} days past due",
                CorrelationId = Guid.NewGuid().ToString(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.ArrearsClassificationHistory.Add(classificationHistory);

            _logger.LogInformation(
                "Loan {LoanId} reclassified from {Previous} to {New} ({DPD} DPD, provision {Provision:C})",
                schedule.LoanId, previousClassification, newClassification, maxDpd, provisionAmount);

            // Send notification if classification is significant
            await _notificationService.SendClassificationNotificationAsync(
                schedule.LoanId,
                schedule.ClientId,
                newClassification,
                maxDpd,
                classificationHistory.CorrelationId!,
                cancellationToken);

            // Audit event
            await _auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "LoanReclassified",
                EntityType = "ArrearsClassification",
                EntityId = classificationHistory.Id.ToString(),
                CorrelationId = classificationHistory.CorrelationId,
                EventData = new
                {
                    LoanId = schedule.LoanId,
                    PreviousClassification = previousClassification,
                    NewClassification = newClassification,
                    DaysPastDue = maxDpd,
                    ProvisionAmount = provisionAmount,
                    IsNonAccrual = isNonAccrual
                }
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ArrearsClassificationHistory>> GetClassificationHistoryAsync(
        Guid loanId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ArrearsClassificationHistory
            .Where(h => h.LoanId == loanId)
            .OrderByDescending(h => h.ClassifiedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetArrearsSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var latestClassifications = await _dbContext.ArrearsClassificationHistory
            .GroupBy(h => h.LoanId)
            .Select(g => g.OrderByDescending(h => h.ClassifiedAt).FirstOrDefault())
            .ToListAsync(cancellationToken);

        var summary = latestClassifications
            .Where(h => h != null)
            .GroupBy(h => h!.NewClassification)
            .ToDictionary(g => g.Key, g => g.Count());

        // Ensure all classifications are present
        foreach (var classification in _bozClassifications.Keys)
        {
            if (!summary.ContainsKey(classification))
            {
                summary[classification] = 0;
            }
        }

        return summary;
    }

    private string DetermineClassification(int dpd)
    {
        if (dpd == 0) return "Current";
        if (dpd >= 1 && dpd <= 89) return "SpecialMention";
        if (dpd >= 90 && dpd <= 179) return "Substandard";
        if (dpd >= 180 && dpd <= 364) return "Doubtful";
        if (dpd >= 365) return "Loss";
        
        return "Current";
    }

    private decimal GetProvisionRate(string classification)
    {
        return _bozClassifications.TryGetValue(classification, out var config)
            ? config.ProvisionRate
            : 0.00m;
    }
}
