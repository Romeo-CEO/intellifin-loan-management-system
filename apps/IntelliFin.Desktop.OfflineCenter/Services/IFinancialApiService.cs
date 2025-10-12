using IntelliFin.Desktop.OfflineCenter.Models;

namespace IntelliFin.Desktop.OfflineCenter.Services;

public interface IFinancialApiService
{
    // Connectivity
    Task<bool> CheckConnectivityAsync();
    
    // Authentication
    Task<bool> AuthenticateAsync(string username, string password);
    Task LogoutAsync();
    Task<bool> RefreshSessionAsync(string? deviceId = null);
    
    // Data Synchronization
    Task<IEnumerable<OfflineLoan>> FetchLoansAsync();
    Task<IEnumerable<OfflineClient>> FetchClientsAsync();
    Task<IEnumerable<OfflinePayment>> FetchPaymentsAsync();
    Task<OfflineFinancialSummary> FetchFinancialSummaryAsync();
    
    // Real-time Data
    Task<DashboardSummary> GetDashboardSummaryAsync();
    Task<decimal> GetAccountBalanceAsync(int accountId);
    Task<IEnumerable<LoanSummary>> GetLoanSummariesAsync();
    
    // Reports
    Task<string> GenerateTrialBalanceReportAsync(DateTime asOfDate);
    Task<string> GenerateBoZReportAsync(DateTime reportDate);
    Task<string> GenerateCollectionsReportAsync(DateTime reportDate);
    
    // Operations
    Task<bool> ProcessPaymentAsync(string loanId, decimal amount, string paymentMethod);
    Task<bool> UpdateLoanStatusAsync(string loanId, string status);
    
    // Health Checks
    Task<bool> CheckFinancialServiceHealthAsync();
    Task<bool> CheckPmecConnectivityAsync();
    Task<bool> CheckPaymentGatewayHealthAsync();
}
