using IntelliFin.Desktop.OfflineCenter.Models;

namespace IntelliFin.Desktop.OfflineCenter.Services;

public interface IOfflineDataService
{
    // Dashboard
    Task<DashboardSummary> GetDashboardSummaryAsync();
    
    // Loans
    Task<IEnumerable<OfflineLoan>> GetLoansAsync();
    Task<OfflineLoan?> GetLoanAsync(string loanId);
    Task<IEnumerable<OfflineLoan>> GetOverdueLoansAsync();
    Task<IEnumerable<LoanSummary>> GetLoanSummariesAsync();
    
    // Clients
    Task<IEnumerable<OfflineClient>> GetClientsAsync();
    Task<OfflineClient?> GetClientAsync(string clientId);
    
    // Payments
    Task<IEnumerable<OfflinePayment>> GetPaymentsAsync();
    Task<IEnumerable<OfflinePayment>> GetPaymentsByLoanAsync(string loanId);
    Task<OfflinePayment?> GetPaymentAsync(string paymentId);
    
    // Financial Summaries
    Task<IEnumerable<OfflineFinancialSummary>> GetFinancialSummariesAsync();
    Task<OfflineFinancialSummary?> GetLatestFinancialSummaryAsync();
    
    // Reports
    Task<IEnumerable<OfflineReport>> GetReportsAsync();
    Task<OfflineReport?> GetReportAsync(string reportId);
    
    // Sync Operations
    Task<IEnumerable<OfflineSyncLog>> GetSyncLogsAsync();
    Task<DateTime?> GetLastSyncTimeAsync();
    Task<bool> HasPendingSyncOperationsAsync();
    
    // Data Management
    Task SaveLoanAsync(OfflineLoan loan);
    Task SaveClientAsync(OfflineClient client);
    Task SavePaymentAsync(OfflinePayment payment);
    Task SaveFinancialSummaryAsync(OfflineFinancialSummary summary);
    Task SaveReportAsync(OfflineReport report);
    Task LogSyncOperationAsync(OfflineSyncLog syncLog);
    
    // Bulk Operations
    Task SaveLoansAsync(IEnumerable<OfflineLoan> loans);
    Task SaveClientsAsync(IEnumerable<OfflineClient> clients);
    Task SavePaymentsAsync(IEnumerable<OfflinePayment> payments);
    
    // Database Management
    Task InitializeDatabaseAsync();
    Task ClearAllDataAsync();
    Task<long> GetDatabaseSizeAsync();
}
