using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

public interface IGeneralLedgerService
{
    Task<decimal> GetAccountBalanceAsync(int accountId, DateTime? asOfDate = null);
    Task<JournalEntryResult> PostJournalEntryAsync(CreateJournalEntryRequest request);
    Task<IEnumerable<GLAccount>> GetAccountsAsync();
    Task<GLAccount?> GetAccountAsync(int accountId);
    Task<IEnumerable<JournalEntry>> GetJournalEntriesAsync(int accountId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<TrialBalanceReport> GenerateTrialBalanceAsync(DateTime asOfDate);
    Task<BoZReport> GenerateBoZReportAsync(DateTime reportDate);
    Task<bool> ValidateJournalEntryAsync(CreateJournalEntryRequest request);
}
