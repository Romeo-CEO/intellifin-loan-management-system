using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.Shared.DomainModels.Repositories;

public interface ILoanApplicationRepository
{
    Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LoanApplication> CreateAsync(LoanApplication application, CancellationToken cancellationToken = default);
    Task<LoanApplication> UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanApplication>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanApplication>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ILoanProductRepository
{
    Task<LoanProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LoanProduct?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanProduct>> GetActiveProductsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanProduct>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
}

public interface ICreditAssessmentRepository
{
    Task<CreditAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CreditAssessment> CreateAsync(CreditAssessment assessment, CancellationToken cancellationToken = default);
    Task<CreditAssessment> UpdateAsync(CreditAssessment assessment, CancellationToken cancellationToken = default);
    Task<CreditAssessment?> GetByLoanApplicationIdAsync(Guid loanApplicationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CreditAssessment>> GetByRiskGradeAsync(string riskGrade, CancellationToken cancellationToken = default);
}

public interface IGLAccountRepository
{
    Task<GLAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GLAccount?> GetByCodeAsync(string accountCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<GLAccount>> GetByTypeAsync(string accountType, CancellationToken cancellationToken = default);
    Task<IEnumerable<GLAccount>> GetActiveByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<GLAccount> UpdateBalanceAsync(Guid accountId, decimal newBalance, CancellationToken cancellationToken = default);
}

public interface IGLEntryRepository
{
    Task<GLEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GLEntry?> GetByEntryNumberAsync(string entryNumber, CancellationToken cancellationToken = default);
    Task<GLEntry> CreateAsync(GLEntry entry, CancellationToken cancellationToken = default);
    Task<IEnumerable<GLEntry>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<GLEntry>> GetByAccountIdAsync(Guid accountId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<string> GenerateEntryNumberAsync(CancellationToken cancellationToken = default);
}