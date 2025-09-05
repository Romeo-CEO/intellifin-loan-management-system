using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

public interface ICollectionsService
{
    Task<CollectionsAccount?> GetCollectionsAccountAsync(string loanId);
    Task<DPDCalculationResult> CalculateDPDAsync(string loanId);
    Task<BoZClassificationResult> ClassifyLoanAsync(string loanId);
    Task<ProvisioningResult> CalculateProvisioningAsync(string loanId);
    Task<IEnumerable<CollectionsAccount>> GetOverdueAccountsAsync();
    Task<DeductionCycleResult> ProcessDeductionCycleAsync(CreateDeductionCycleRequest request);
    Task<PaymentResult> RecordPaymentAsync(RecordPaymentRequest request);
    Task<CollectionsReport> GenerateCollectionsReportAsync(DateTime reportDate);
    Task<bool> UpdateAccountStatusAsync(string loanId, CollectionsStatus status);
}
