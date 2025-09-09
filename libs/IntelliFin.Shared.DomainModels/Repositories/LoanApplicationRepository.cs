using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Shared.DomainModels.Repositories;

public class LoanApplicationRepository : ILoanApplicationRepository
{
    private readonly LmsDbContext _context;

    public LoanApplicationRepository(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Client)
            .Include(x => x.Product)
            .Include(x => x.CreditAssessments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<LoanApplication> CreateAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        _context.LoanApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task<LoanApplication> UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        _context.LoanApplications.Update(application);
        await _context.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task<IEnumerable<LoanApplication>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Product)
            .Include(x => x.CreditAssessments)
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LoanApplication>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _context.LoanApplications
            .Include(x => x.Client)
            .Include(x => x.Product)
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LoanApplications.AnyAsync(x => x.Id == id, cancellationToken);
    }
}

public class LoanProductRepository : ILoanProductRepository
{
    private readonly LmsDbContext _context;

    public LoanProductRepository(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<LoanProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LoanProducts
            .Include(x => x.RequiredFields)
            .Include(x => x.ValidationRules)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<LoanProduct?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.LoanProducts
            .Include(x => x.RequiredFields)
            .Include(x => x.ValidationRules)
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
    }

    public async Task<IEnumerable<LoanProduct>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LoanProducts
            .Include(x => x.RequiredFields)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LoanProduct>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.LoanProducts
            .Include(x => x.RequiredFields)
            .Where(x => x.Category == category && x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}

public class CreditAssessmentRepository : ICreditAssessmentRepository
{
    private readonly LmsDbContext _context;

    public CreditAssessmentRepository(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<CreditAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CreditAssessments
            .Include(x => x.LoanApplication)
            .Include(x => x.CreditFactors)
            .Include(x => x.RiskIndicators)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<CreditAssessment> CreateAsync(CreditAssessment assessment, CancellationToken cancellationToken = default)
    {
        _context.CreditAssessments.Add(assessment);
        await _context.SaveChangesAsync(cancellationToken);
        return assessment;
    }

    public async Task<CreditAssessment> UpdateAsync(CreditAssessment assessment, CancellationToken cancellationToken = default)
    {
        _context.CreditAssessments.Update(assessment);
        await _context.SaveChangesAsync(cancellationToken);
        return assessment;
    }

    public async Task<CreditAssessment?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken cancellationToken = default)
    {
        return await _context.CreditAssessments
            .Include(x => x.CreditFactors)
            .Include(x => x.RiskIndicators)
            .Where(x => x.LoanApplicationId == loanApplicationId)
            .OrderByDescending(x => x.AssessedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<CreditAssessment>> GetByRiskGradeAsync(string riskGrade, CancellationToken cancellationToken = default)
    {
        return await _context.CreditAssessments
            .Include(x => x.LoanApplication)
            .Where(x => x.RiskGrade == riskGrade)
            .OrderByDescending(x => x.AssessedAt)
            .ToListAsync(cancellationToken);
    }
}

public class GLAccountRepository : IGLAccountRepository
{
    private readonly LmsDbContext _context;

    public GLAccountRepository(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<GLAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GLAccounts
            .Include(x => x.ParentAccount)
            .Include(x => x.SubAccounts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<GLAccount?> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default)
    {
        return await _context.GLAccounts
            .Include(x => x.ParentAccount)
            .Include(x => x.SubAccounts)
            .FirstOrDefaultAsync(x => x.AccountCode == accountCode, cancellationToken);
    }

    public async Task<IEnumerable<GLAccount>> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default)
    {
        return await _context.GLAccounts
            .Where(x => x.AccountType == accountType && x.IsActive)
            .OrderBy(x => x.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<GLAccount>> GetActiveByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.GLAccounts
            .Where(x => x.Category == category && x.IsActive)
            .OrderBy(x => x.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<GLAccount> UpdateBalanceAsync(Guid accountId, decimal newBalance, CancellationToken cancellationToken = default)
    {
        var account = await _context.GLAccounts.FindAsync(new object[] { accountId }, cancellationToken);
        if (account == null)
            throw new KeyNotFoundException($"GL Account {accountId} not found");

        account.CurrentBalance = newBalance;
        account.LastModified = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        return account;
    }
}

public class GLEntryRepository : IGLEntryRepository
{
    private readonly LmsDbContext _context;

    public GLEntryRepository(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<GLEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GLEntries
            .Include(x => x.Lines)
            .ThenInclude(l => l.GLAccount)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<GLEntry?> GetByEntryNumberAsync(string entryNumber, CancellationToken cancellationToken = default)
    {
        return await _context.GLEntries
            .Include(x => x.Lines)
            .ThenInclude(l => l.GLAccount)
            .FirstOrDefaultAsync(x => x.EntryNumber == entryNumber, cancellationToken);
    }

    public async Task<GLEntry> CreateAsync(GLEntry entry, CancellationToken cancellationToken = default)
    {
        _context.GLEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<IEnumerable<GLEntry>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        return await _context.GLEntries
            .Include(x => x.Lines)
            .ThenInclude(l => l.GLAccount)
            .Where(x => x.TransactionDate >= fromDate && x.TransactionDate <= toDate)
            .OrderByDescending(x => x.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<GLEntry>> GetByAccountIdAsync(Guid accountId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.GLEntries
            .Include(x => x.Lines)
            .ThenInclude(l => l.GLAccount)
            .Where(x => x.Lines.Any(l => l.GLAccountId == accountId));

        if (fromDate.HasValue)
            query = query.Where(x => x.TransactionDate >= fromDate);
        
        if (toDate.HasValue)
            query = query.Where(x => x.TransactionDate <= toDate);

        return await query
            .OrderByDescending(x => x.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GenerateEntryNumberAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var prefix = $"GL{today:yyyyMMdd}";
        
        var lastEntry = await _context.GLEntries
            .Where(x => x.EntryNumber.StartsWith(prefix))
            .OrderByDescending(x => x.EntryNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = 1;
        if (lastEntry != null)
        {
            var lastSequence = lastEntry.EntryNumber.Substring(prefix.Length);
            if (int.TryParse(lastSequence, out var num))
                sequence = num + 1;
        }

        return $"{prefix}{sequence:D4}";
    }
}

public class DocumentVerificationRepository : IDocumentVerificationRepository
{
    private readonly LmsDbContext _context;

    public DocumentVerificationRepository(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentVerification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentVerifications
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<DocumentVerification?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentVerifications
            .Include(x => x.Client)
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<DocumentVerification>> GetByClientIdAllAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentVerifications
            .Include(x => x.Client)
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentVerification> CreateAsync(DocumentVerification verification, CancellationToken cancellationToken = default)
    {
        _context.DocumentVerifications.Add(verification);
        await _context.SaveChangesAsync(cancellationToken);
        return verification;
    }

    public async Task<DocumentVerification> UpdateAsync(DocumentVerification verification, CancellationToken cancellationToken = default)
    {
        verification.LastModified = DateTime.UtcNow;
        _context.DocumentVerifications.Update(verification);
        await _context.SaveChangesAsync(cancellationToken);
        return verification;
    }

    public async Task<IEnumerable<DocumentVerification>> GetPendingVerificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DocumentVerifications
            .Include(x => x.Client)
            .Where(x => !x.IsVerified || x.VerificationDate == null)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DocumentVerification>> GetVerificationsByOfficerAsync(string officerId, DateTime? fromDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentVerifications
            .Include(x => x.Client)
            .Where(x => x.VerifiedBy == officerId && x.IsVerified);

        if (fromDate.HasValue)
            query = query.Where(x => x.VerificationDate >= fromDate);

        return await query
            .OrderByDescending(x => x.VerificationDate)
            .ToListAsync(cancellationToken);
    }
}