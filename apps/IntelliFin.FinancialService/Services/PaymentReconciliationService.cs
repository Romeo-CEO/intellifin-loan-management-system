using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace IntelliFin.FinancialService.Services;

public interface IPaymentReconciliationService
{
    Task<ReconciliationSummary> ReconcilePaymentsAsync(DateTime reconciliationDate);
    Task<ReconciliationSummary> ReconcileTinggPaymentsAsync(DateTime reconciliationDate);
    Task<ReconciliationSummary> ReconcilePmecDeductionsAsync(DateTime reconciliationDate);
    Task<ReconciliationResult> ReconcileSpecificPaymentAsync(string paymentId, string externalReference);
    Task ScheduleAutomaticReconciliationAsync();
    Task<IEnumerable<UnreconciledPayment>> GetUnreconciledPaymentsAsync(int daysBack = 7);
}

public class PaymentReconciliationService : IPaymentReconciliationService
{
    private readonly IPaymentProcessingService _paymentService;
    private readonly IPmecService _pmecService;
    private readonly IGLEntryRepository _glEntryRepository;
    private readonly ILogger<PaymentReconciliationService> _logger;
    private readonly IConfiguration _configuration;

    public PaymentReconciliationService(
        IPaymentProcessingService paymentService,
        IPmecService pmecService,
        IGLEntryRepository glEntryRepository,
        ILogger<PaymentReconciliationService> logger,
        IConfiguration configuration)
    {
        _paymentService = paymentService;
        _pmecService = pmecService;
        _glEntryRepository = glEntryRepository;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ReconciliationSummary> ReconcilePaymentsAsync(DateTime reconciliationDate)
    {
        _logger.LogInformation("Starting payment reconciliation for date: {ReconciliationDate}", reconciliationDate);

        var summary = new ReconciliationSummary
        {
            ReconciliationDate = reconciliationDate,
            StartTime = DateTime.UtcNow,
            Type = ReconciliationType.AllPayments
        };

        try
        {
            // Get all payments for the reconciliation date
            var payments = await GetPaymentsForDateAsync(reconciliationDate);
            summary.TotalRecords = payments.Count();

            var reconciledPayments = new List<ReconciliationResult>();
            var unreconciledPayments = new List<UnreconciledPayment>();

            foreach (var payment in payments)
            {
                try
                {
                    var reconciliationResult = await ReconcileSpecificPaymentAsync(payment.Id, payment.ExternalReference);
                    
                    if (reconciliationResult.IsReconciled)
                    {
                        reconciledPayments.Add(reconciliationResult);
                        summary.ReconciledAmount += reconciliationResult.ReconciledAmount;
                    }
                    else
                    {
                        unreconciledPayments.Add(new UnreconciledPayment
                        {
                            PaymentId = payment.Id,
                            LoanId = payment.LoanId,
                            Amount = payment.Amount,
                            PaymentDate = payment.PaymentDate,
                            ExternalReference = payment.ExternalReference,
                            Reason = reconciliationResult.FailureReason
                        });
                        summary.UnreconciledAmount += payment.Amount;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reconciling payment {PaymentId}", payment.Id);
                    unreconciledPayments.Add(new UnreconciledPayment
                    {
                        PaymentId = payment.Id,
                        LoanId = payment.LoanId,
                        Amount = payment.Amount,
                        PaymentDate = payment.PaymentDate,
                        ExternalReference = payment.ExternalReference,
                        Reason = $"Reconciliation error: {ex.Message}"
                    });
                    summary.UnreconciledAmount += payment.Amount;
                }
            }

            summary.ReconciledRecords = reconciledPayments.Count;
            summary.UnreconciledRecords = unreconciledPayments.Count;
            summary.ReconciliationRate = summary.TotalRecords > 0 
                ? (decimal)summary.ReconciledRecords / summary.TotalRecords * 100 
                : 0;

            summary.EndTime = DateTime.UtcNow;
            summary.Duration = summary.EndTime - summary.StartTime;
            summary.Status = ReconciliationStatus.Completed;

            // Log summary
            _logger.LogInformation("Payment reconciliation completed. " +
                "Total: {Total}, Reconciled: {Reconciled}, Unreconciled: {Unreconciled}, " +
                "Rate: {Rate:F2}%, Duration: {Duration}",
                summary.TotalRecords, summary.ReconciledRecords, summary.UnreconciledRecords,
                summary.ReconciliationRate, summary.Duration);

            // Store reconciliation results for reporting
            await StoreReconciliationResultsAsync(summary, reconciledPayments, unreconciledPayments);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment reconciliation failed for date: {ReconciliationDate}", reconciliationDate);
            
            summary.EndTime = DateTime.UtcNow;
            summary.Duration = summary.EndTime - summary.StartTime;
            summary.Status = ReconciliationStatus.Failed;
            summary.ErrorMessage = ex.Message;
            
            return summary;
        }
    }

    public async Task<ReconciliationSummary> ReconcileTinggPaymentsAsync(DateTime reconciliationDate)
    {
        _logger.LogInformation("Starting Tingg payment reconciliation for date: {ReconciliationDate}", reconciliationDate);

        var summary = new ReconciliationSummary
        {
            ReconciliationDate = reconciliationDate,
            StartTime = DateTime.UtcNow,
            Type = ReconciliationType.TinggPayments
        };

        try
        {
            // Get Tingg-specific reconciliation file or API data
            var tinggTransactions = await FetchTinggTransactionsAsync(reconciliationDate);
            var ourPayments = await GetTinggPaymentsForDateAsync(reconciliationDate);

            summary.TotalRecords = ourPayments.Count();

            var matchedTransactions = 0;
            var reconciledAmount = 0m;

            foreach (var payment in ourPayments)
            {
                var matchingTransaction = tinggTransactions.FirstOrDefault(t => 
                    t.MerchantTransactionId == payment.ExternalReference ||
                    t.TinggTransactionId == payment.ProcessorReference);

                if (matchingTransaction != null && matchingTransaction.Amount == payment.Amount)
                {
                    matchedTransactions++;
                    reconciledAmount += payment.Amount;
                    
                    // Update payment status if needed
                    await UpdatePaymentReconciliationStatusAsync(payment.Id, true, 
                        matchingTransaction.TinggTransactionId);
                }
                else
                {
                    summary.UnreconciledAmount += payment.Amount;
                }
            }

            summary.ReconciledRecords = matchedTransactions;
            summary.UnreconciledRecords = summary.TotalRecords - matchedTransactions;
            summary.ReconciledAmount = reconciledAmount;
            summary.ReconciliationRate = summary.TotalRecords > 0 
                ? (decimal)summary.ReconciledRecords / summary.TotalRecords * 100 
                : 0;

            summary.EndTime = DateTime.UtcNow;
            summary.Duration = summary.EndTime - summary.StartTime;
            summary.Status = ReconciliationStatus.Completed;

            _logger.LogInformation("Tingg reconciliation completed. Rate: {Rate:F2}%", summary.ReconciliationRate);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tingg reconciliation failed");
            
            summary.EndTime = DateTime.UtcNow;
            summary.Duration = summary.EndTime - summary.StartTime;
            summary.Status = ReconciliationStatus.Failed;
            summary.ErrorMessage = ex.Message;
            
            return summary;
        }
    }

    public async Task<ReconciliationSummary> ReconcilePmecDeductionsAsync(DateTime reconciliationDate)
    {
        _logger.LogInformation("Starting PMEC deduction reconciliation for date: {ReconciliationDate}", reconciliationDate);

        var summary = new ReconciliationSummary
        {
            ReconciliationDate = reconciliationDate,
            StartTime = DateTime.UtcNow,
            Type = ReconciliationType.PmecDeductions
        };

        try
        {
            // Get the relevant PMEC cycle for the date
            var cycleId = GeneratePmecCycleId(reconciliationDate);
            var pmecResults = await _pmecService.FetchDeductionResultsAsync(cycleId);
            var ourDeductions = await GetPmecDeductionsForCycleAsync(cycleId);

            summary.TotalRecords = ourDeductions.Count();

            var matchedDeductions = 0;
            var reconciledAmount = 0m;

            foreach (var deduction in ourDeductions)
            {
                var pmecResult = pmecResults.Results?.FirstOrDefault(r => 
                    r.EmployeeId == deduction.EmployeeId && 
                    r.LoanId == deduction.LoanId);

                if (pmecResult != null)
                {
                    if (pmecResult.Status == DeductionStatus.Processed && 
                        pmecResult.ProcessedAmount == deduction.Amount)
                    {
                        matchedDeductions++;
                        reconciledAmount += deduction.Amount;
                        
                        // Update deduction status
                        await UpdateDeductionReconciliationStatusAsync(deduction.Id, true, 
                            pmecResult.ExternalReference);
                    }
                    else
                    {
                        // Handle partial processing or failures
                        summary.UnreconciledAmount += deduction.Amount - pmecResult.ProcessedAmount;
                    }
                }
                else
                {
                    summary.UnreconciledAmount += deduction.Amount;
                }
            }

            summary.ReconciledRecords = matchedDeductions;
            summary.UnreconciledRecords = summary.TotalRecords - matchedDeductions;
            summary.ReconciledAmount = reconciledAmount;
            summary.ReconciliationRate = summary.TotalRecords > 0 
                ? (decimal)summary.ReconciledRecords / summary.TotalRecords * 100 
                : 0;

            summary.EndTime = DateTime.UtcNow;
            summary.Duration = summary.EndTime - summary.StartTime;
            summary.Status = ReconciliationStatus.Completed;

            _logger.LogInformation("PMEC reconciliation completed. Rate: {Rate:F2}%", summary.ReconciliationRate);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PMEC reconciliation failed");
            
            summary.EndTime = DateTime.UtcNow;
            summary.Duration = summary.EndTime - summary.StartTime;
            summary.Status = ReconciliationStatus.Failed;
            summary.ErrorMessage = ex.Message;
            
            return summary;
        }
    }

    public async Task<ReconciliationResult> ReconcileSpecificPaymentAsync(string paymentId, string externalReference)
    {
        _logger.LogInformation("Reconciling specific payment {PaymentId} with reference {ExternalReference}", 
            paymentId, externalReference);

        try
        {
            // Get payment details
            var paymentStatus = await _paymentService.GetPaymentStatusAsync(paymentId);
            var reconciliationResult = await _paymentService.ReconcilePaymentAsync(paymentId);

            var result = new ReconciliationResult
            {
                PaymentId = paymentId,
                ExternalReference = externalReference,
                ReconciliationDate = DateTime.UtcNow,
                IsReconciled = reconciliationResult.IsReconciled,
                ReconciledAmount = reconciliationResult.ReconciledAmount,
                ReconciliationReference = reconciliationResult.ReconciliationReference
            };

            if (!result.IsReconciled)
            {
                result.FailureReason = "Payment not found in external system or amounts don't match";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile payment {PaymentId}", paymentId);
            
            return new ReconciliationResult
            {
                PaymentId = paymentId,
                ExternalReference = externalReference,
                ReconciliationDate = DateTime.UtcNow,
                IsReconciled = false,
                FailureReason = ex.Message
            };
        }
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ScheduleAutomaticReconciliationAsync()
    {
        _logger.LogInformation("Scheduling automatic payment reconciliation");

        try
        {
            // Schedule daily reconciliation at configured time (default 6 AM)
            var reconciliationTime = _configuration.GetValue<string>("Reconciliation:DailyTime", "06:00");
            
            RecurringJob.AddOrUpdate<IPaymentReconciliationService>(
                "daily-payment-reconciliation",
                service => service.ReconcilePaymentsAsync(DateTime.Today.AddDays(-1)),
                $"0 {reconciliationTime.Split(':')[1]} {reconciliationTime.Split(':')[0]} * * *", // Cron expression
                TimeZoneInfo.FindSystemTimeZoneById("Africa/Lusaka"));

            // Schedule weekly Tingg reconciliation
            RecurringJob.AddOrUpdate<IPaymentReconciliationService>(
                "weekly-tingg-reconciliation",
                service => service.ReconcileTinggPaymentsAsync(DateTime.Today.AddDays(-7)),
                "0 0 7 ? * SUN", // Every Sunday at 7 AM
                TimeZoneInfo.FindSystemTimeZoneById("Africa/Lusaka"));

            // Schedule monthly PMEC reconciliation
            RecurringJob.AddOrUpdate<IPaymentReconciliationService>(
                "monthly-pmec-reconciliation",
                service => service.ReconcilePmecDeductionsAsync(DateTime.Today.AddDays(-30)),
                "0 0 8 1 * ?", // 1st day of month at 8 AM
                TimeZoneInfo.FindSystemTimeZoneById("Africa/Lusaka"));

            _logger.LogInformation("Automatic reconciliation jobs scheduled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule automatic reconciliation");
            throw;
        }
    }

    public async Task<IEnumerable<UnreconciledPayment>> GetUnreconciledPaymentsAsync(int daysBack = 7)
    {
        _logger.LogInformation("Getting unreconciled payments for the last {DaysBack} days", daysBack);

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
            
            // This would typically query a database table tracking reconciliation status
            // For now, simulating with sample data
            var unreconciledPayments = new List<UnreconciledPayment>
            {
                new UnreconciledPayment
                {
                    PaymentId = Guid.NewGuid().ToString(),
                    LoanId = "LOAN-001",
                    Amount = 500.00m,
                    PaymentDate = DateTime.UtcNow.AddDays(-2),
                    ExternalReference = "TINGG-123456",
                    Reason = "No matching transaction in Tingg settlement file",
                    DaysUnreconciled = 2
                },
                new UnreconciledPayment
                {
                    PaymentId = Guid.NewGuid().ToString(),
                    LoanId = "LOAN-002",
                    Amount = 750.00m,
                    PaymentDate = DateTime.UtcNow.AddDays(-5),
                    ExternalReference = "PMEC-789012",
                    Reason = "Amount mismatch with PMEC deduction result",
                    DaysUnreconciled = 5
                }
            };

            return unreconciledPayments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unreconciled payments");
            return new List<UnreconciledPayment>();
        }
    }

    #region Private Helper Methods

    private async Task<IEnumerable<Payment>> GetPaymentsForDateAsync(DateTime date)
    {
        // This would typically query the database
        // Simulating for now
        await Task.Delay(50);
        return new List<Payment>();
    }

    private async Task<IEnumerable<Payment>> GetTinggPaymentsForDateAsync(DateTime date)
    {
        await Task.Delay(50);
        return new List<Payment>();
    }

    private async Task<IEnumerable<DeductionItem>> GetPmecDeductionsForCycleAsync(string cycleId)
    {
        await Task.Delay(50);
        return new List<DeductionItem>();
    }

    private async Task<IEnumerable<TinggTransaction>> FetchTinggTransactionsAsync(DateTime date)
    {
        // This would call Tingg API or process settlement file
        await Task.Delay(100);
        return new List<TinggTransaction>();
    }

    private string GeneratePmecCycleId(DateTime date)
    {
        return $"PMEC-{date:yyyy-MM}";
    }

    private async Task UpdatePaymentReconciliationStatusAsync(string paymentId, bool isReconciled, string externalReference)
    {
        // Update payment record in database
        await Task.Delay(10);
    }

    private async Task UpdateDeductionReconciliationStatusAsync(string deductionId, bool isReconciled, string externalReference)
    {
        // Update deduction record in database
        await Task.Delay(10);
    }

    private async Task StoreReconciliationResultsAsync(ReconciliationSummary summary, 
        List<ReconciliationResult> reconciled, List<UnreconciledPayment> unreconciled)
    {
        // Store reconciliation results in database for reporting and audit
        await Task.Delay(50);
    }

    #endregion
}

// Additional models for reconciliation
public class TinggTransaction
{
    public string TinggTransactionId { get; set; } = string.Empty;
    public string MerchantTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ReconciliationSummary
{
    public DateTime ReconciliationDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public ReconciliationType Type { get; set; }
    public int TotalRecords { get; set; }
    public int ReconciledRecords { get; set; }
    public int UnreconciledRecords { get; set; }
    public decimal ReconciledAmount { get; set; }
    public decimal UnreconciledAmount { get; set; }
    public decimal ReconciliationRate { get; set; }
    public ReconciliationStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ReconciliationResult
{
    public string PaymentId { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public DateTime ReconciliationDate { get; set; }
    public bool IsReconciled { get; set; }
    public decimal ReconciledAmount { get; set; }
    public string? ReconciliationReference { get; set; }
    public string? FailureReason { get; set; }
}

public class UnreconciledPayment
{
    public string PaymentId { get; set; } = string.Empty;
    public string LoanId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int DaysUnreconciled { get; set; }
}

public enum ReconciliationType
{
    AllPayments,
    TinggPayments,
    PmecDeductions,
    BankTransfers
}

public enum ReconciliationStatus
{
    InProgress,
    Completed,
    Failed,
    PartiallyCompleted
}