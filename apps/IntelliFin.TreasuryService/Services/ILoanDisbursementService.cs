using IntelliFin.TreasuryService.Models;

namespace IntelliFin.TreasuryService.Services;

public interface ILoanDisbursementService
{
    Task<LoanDisbursement> GetByIdAsync(int id);
    Task<LoanDisbursement> GetByDisbursementIdAsync(Guid disbursementId);
    Task<IEnumerable<LoanDisbursement>> GetByStatusAsync(string status);
    Task<LoanDisbursement> CreateAsync(LoanDisbursement disbursement);
    Task UpdateStatusAsync(Guid disbursementId, string status, string processedBy);
    Task<DisbursementApproval> AddApprovalAsync(Guid disbursementId, string approverId, string approverName, int approvalLevel, string decision, string comments);
}

