using IntelliFin.TreasuryService.Models;
using IntelliFin.TreasuryService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.TreasuryService.Services;

public class LoanDisbursementService : ILoanDisbursementService
{
    private readonly TreasuryDbContext _context;
    private readonly ILogger<LoanDisbursementService> _logger;

    public LoanDisbursementService(
        TreasuryDbContext context,
        ILogger<LoanDisbursementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<LoanDisbursement> GetByIdAsync(int id)
    {
        return await _context.LoanDisbursements
            .Include(ld => ld.Approvals)
            .FirstOrDefaultAsync(ld => ld.Id == id);
    }

    public async Task<LoanDisbursement> GetByDisbursementIdAsync(Guid disbursementId)
    {
        return await _context.LoanDisbursements
            .Include(ld => ld.Approvals)
            .FirstOrDefaultAsync(ld => ld.DisbursementId == disbursementId);
    }

    public async Task<IEnumerable<LoanDisbursement>> GetByStatusAsync(string status)
    {
        return await _context.LoanDisbursements
            .Where(ld => ld.Status == status)
            .OrderByDescending(ld => ld.RequestedAt)
            .ToListAsync();
    }

    public async Task<LoanDisbursement> CreateAsync(LoanDisbursement disbursement)
    {
        _logger.LogInformation("Creating loan disbursement: DisbursementId={DisbursementId}, LoanId={LoanId}, Amount={Amount}",
            disbursement.DisbursementId, disbursement.LoanId, disbursement.Amount);

        _context.LoanDisbursements.Add(disbursement);
        await _context.SaveChangesAsync();
        return disbursement;
    }

    public async Task UpdateStatusAsync(Guid disbursementId, string status, string processedBy)
    {
        _logger.LogInformation("Updating disbursement status: DisbursementId={DisbursementId}, Status={Status}",
            disbursementId, status);

        var disbursement = await _context.LoanDisbursements
            .FirstOrDefaultAsync(ld => ld.DisbursementId == disbursementId);

        if (disbursement != null)
        {
            disbursement.Status = status;
            if (status == "Processed" || status == "Failed")
            {
                disbursement.ProcessedAt = DateTime.UtcNow;
                disbursement.ProcessedBy = processedBy;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task<DisbursementApproval> AddApprovalAsync(Guid disbursementId, string approverId, string approverName, int approvalLevel, string decision, string comments)
    {
        _logger.LogInformation("Adding disbursement approval: DisbursementId={DisbursementId}, Approver={ApproverId}, Decision={Decision}",
            disbursementId, approverId, decision);

        var approval = new DisbursementApproval
        {
            DisbursementId = disbursementId,
            ApproverId = approverId,
            ApproverName = approverName,
            ApprovalLevel = approvalLevel,
            Decision = decision,
            Comments = comments,
            ApprovedAt = DateTime.UtcNow
        };

        _context.DisbursementApprovals.Add(approval);
        await _context.SaveChangesAsync();
        return approval;
    }
}
