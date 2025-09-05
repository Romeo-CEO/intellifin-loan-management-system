using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

public interface IPmecService
{
    Task<EmployeeVerificationResult> VerifyEmployeeAsync(EmployeeVerificationRequest request);
    Task<DeductionSubmissionResult> SubmitDeductionsAsync(DeductionSubmissionRequest request);
    Task<DeductionResultsResponse> FetchDeductionResultsAsync(string cycleId);
    Task<bool> ValidateEmployeeEligibilityAsync(string employeeId, string nationalId);
    Task<IEnumerable<DeductionItem>> GetPendingDeductionsAsync();
    Task<DeductionStatusResult> GetDeductionStatusAsync(string deductionId);
    Task<bool> CancelDeductionAsync(string deductionId, string reason);
    Task<PmecHealthCheckResult> CheckPmecConnectivityAsync();
}
