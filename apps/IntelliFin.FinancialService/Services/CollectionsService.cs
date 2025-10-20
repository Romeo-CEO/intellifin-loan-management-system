using IntelliFin.FinancialService.Exceptions;
using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Logging;

namespace IntelliFin.FinancialService.Services;

public class CollectionsService : ICollectionsService
{
    private readonly ILogger<CollectionsService> _logger;
    private readonly IAuditClient _auditClient;

    public CollectionsService(ILogger<CollectionsService> logger, IAuditClient auditClient)
    {
        _logger = logger;
        _auditClient = auditClient;
    }

    public async Task<CollectionsAccount?> GetCollectionsAccountAsync(string loanId)
    {
        _logger.LogInformation("Getting collections account for loan {LoanId}", loanId);
        
        // TODO: Implement actual database query
        await Task.Delay(20);
        
        return new CollectionsAccount
        {
            LoanId = loanId,
            ClientId = "CLIENT-001",
            PrincipalBalance = 5000.00m,
            InterestBalance = 250.00m,
            FeesBalance = 50.00m,
            TotalBalance = 5300.00m,
            LastPaymentDate = DateTime.UtcNow.AddDays(-30),
            LastPaymentAmount = 500.00m,
            DaysPastDue = 15,
            Status = CollectionsStatus.EarlyDelinquency,
            BoZClassification = BoZClassification.Normal,
            ProvisionAmount = 0.00m,
            NextDueDate = DateTime.UtcNow.AddDays(15),
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<DPDCalculationResult> CalculateDPDAsync(string loanId)
    {
        _logger.LogInformation("Calculating DPD for loan {LoanId}", loanId);
        
        // TODO: Implement actual DPD calculation logic
        await Task.Delay(30);
        
        var lastDueDate = DateTime.UtcNow.AddDays(-15);
        var daysPastDue = (DateTime.UtcNow.Date - lastDueDate.Date).Days;
        
        return new DPDCalculationResult
        {
            LoanId = loanId,
            DaysPastDue = Math.Max(0, daysPastDue),
            CalculationDate = DateTime.UtcNow,
            LastDueDate = lastDueDate,
            AmountOverdue = 500.00m,
            CalculationMethod = "Standard DPD Calculation"
        };
    }

    public async Task<BoZClassificationResult> ClassifyLoanAsync(string loanId)
    {
        _logger.LogInformation("Classifying loan {LoanId} according to BoZ guidelines", loanId);
        
        // Get DPD first
        var dpdResult = await CalculateDPDAsync(loanId);
        
        // BoZ classification based on DPD
        var classification = dpdResult.DaysPastDue switch
        {
            <= 30 => BoZClassification.Normal,
            <= 90 => BoZClassification.SpecialMention,
            <= 180 => BoZClassification.Substandard,
            <= 365 => BoZClassification.Doubtful,
            _ => BoZClassification.Loss
        };
        
        var provisionRate = classification switch
        {
            BoZClassification.Normal => 0.01m,
            BoZClassification.SpecialMention => 0.05m,
            BoZClassification.Substandard => 0.20m,
            BoZClassification.Doubtful => 0.50m,
            BoZClassification.Loss => 1.00m,
            _ => 0.01m
        };
        
        return new BoZClassificationResult
        {
            LoanId = loanId,
            Classification = classification,
            ProvisionRate = provisionRate,
            Reason = $"DPD: {dpdResult.DaysPastDue} days",
            ClassificationDate = DateTime.UtcNow
        };
    }

    public async Task<ProvisioningResult> CalculateProvisioningAsync(string loanId)
    {
        _logger.LogInformation("Calculating provisioning for loan {LoanId}", loanId);
        
        var account = await GetCollectionsAccountAsync(loanId);
        var classification = await ClassifyLoanAsync(loanId);
        
        if (account == null)
        {
            throw new ArgumentException($"Loan {loanId} not found");
        }
        
        var provisionAmount = account.TotalBalance * classification.ProvisionRate;
        
        return new ProvisioningResult
        {
            LoanId = loanId,
            ProvisionAmount = provisionAmount,
            ProvisionRate = classification.ProvisionRate,
            Classification = classification.Classification,
            CalculationDate = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<CollectionsAccount>> GetOverdueAccountsAsync()
    {
        _logger.LogInformation("Getting all overdue accounts");
        
        // TODO: Implement actual database query
        await Task.Delay(50);
        
        return new List<CollectionsAccount>
        {
            new CollectionsAccount
            {
                LoanId = "LOAN-001",
                ClientId = "CLIENT-001",
                TotalBalance = 5300.00m,
                DaysPastDue = 15,
                Status = CollectionsStatus.EarlyDelinquency
            },
            new CollectionsAccount
            {
                LoanId = "LOAN-002",
                ClientId = "CLIENT-002",
                TotalBalance = 8500.00m,
                DaysPastDue = 45,
                Status = CollectionsStatus.Delinquent
            }
        };
    }

    public async Task<DeductionCycleResult> ProcessDeductionCycleAsync(CreateDeductionCycleRequest request)
    {
        _logger.LogInformation("Processing deduction cycle for period {Period}", request.Period);

        // TODO: Implement actual deduction cycle processing
        await Task.Delay(100);

        var result = new DeductionCycleResult
        {
            CycleId = Guid.NewGuid().ToString(),
            Period = request.Period,
            TotalItems = request.LoanIds.Count,
            ProcessedItems = request.LoanIds.Count,
            TotalAmount = request.LoanIds.Count * 500.00m,
            Status = DeductionCycleStatus.Completed
        };

        await ForwardAuditAsync(
            "system",
            "CollectionsDeductionCycleProcessed",
            "CollectionsDeductionCycle",
            result.CycleId,
            new
            {
                request.Period,
                result.TotalItems,
                result.TotalAmount
            });

        return result;
    }

    public async Task<PaymentResult> RecordPaymentAsync(RecordPaymentRequest request)
    {
        _logger.LogInformation("Recording payment for loan {LoanId}, amount {Amount}", request.LoanId, request.Amount);

        // TODO: Implement actual payment recording
        await Task.Delay(30);

        var result = new PaymentResult
        {
            Success = true,
            PaymentId = Guid.NewGuid().ToString(),
            Message = "Payment recorded successfully"
        };

        await ForwardAuditAsync(
            "system",
            "CollectionsPaymentRecorded",
            "CollectionsPayment",
            result.PaymentId!,
            new
            {
                request.LoanId,
                request.Amount,
                Method = request.Method.ToString(),
                request.ExternalReference
            });

        return result;
    }

    public async Task<CollectionsReport> GenerateCollectionsReportAsync(DateTime reportDate)
    {
        _logger.LogInformation("Generating collections report for {ReportDate}", reportDate);
        
        // TODO: Implement actual report generation
        await Task.Delay(150);
        
        return new CollectionsReport
        {
            ReportDate = reportDate,
            TotalAccounts = 100,
            TotalOutstanding = 500000.00m,
            ClassificationBreakdown = new Dictionary<BoZClassification, int>
            {
                { BoZClassification.Normal, 80 },
                { BoZClassification.SpecialMention, 15 },
                { BoZClassification.Substandard, 5 }
            },
            StatusBreakdown = new Dictionary<CollectionsStatus, int>
            {
                { CollectionsStatus.Current, 75 },
                { CollectionsStatus.EarlyDelinquency, 20 },
                { CollectionsStatus.Delinquent, 5 }
            },
            TotalProvisions = 25000.00m
        };
    }

    public async Task<bool> UpdateAccountStatusAsync(string loanId, CollectionsStatus status)
    {
        _logger.LogInformation("Updating account status for loan {LoanId} to {Status}", loanId, status);

        // TODO: Implement actual database update
        await Task.Delay(20);

        return true;
    }

    private async Task ForwardAuditAsync(string actor, string action, string entityType, string entityId, object eventData)
    {
        var payload = new AuditEventPayload
        {
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EventData = eventData,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _auditClient.LogEventAsync(payload);
        }
        catch (Exception ex)
        {
            throw new AuditForwardingException("Failed to forward audit event to Admin Service", ex);
        }
    }
}
