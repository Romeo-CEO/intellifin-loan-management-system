using IntelliFin.Desktop.OfflineCenter.Data;
using IntelliFin.Desktop.OfflineCenter.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Desktop.OfflineCenter.Services;

public class OfflineDataService : IOfflineDataService
{
    private readonly OfflineDbContext _context;

    public OfflineDataService(OfflineDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        var loans = await _context.Loans.ToListAsync();
        var payments = await _context.Payments.Where(p => p.PaymentDate.Month == DateTime.Now.Month).ToListAsync();
        var latestSummary = await GetLatestFinancialSummaryAsync();

        return new DashboardSummary
        {
            TotalPortfolioValue = loans.Sum(l => l.OutstandingBalance),
            TotalActiveLoans = loans.Count(l => l.Status == "Active"),
            TotalClients = await _context.Clients.CountAsync(),
            MonthlyCollections = payments.Where(p => p.Status == "Completed").Sum(p => p.Amount),
            MonthlyDisbursements = latestSummary?.TotalDisbursements ?? 0,
            OverdueLoans = loans.Count(l => l.DaysPastDue > 0),
            OverdueAmount = loans.Where(l => l.DaysPastDue > 0).Sum(l => l.OutstandingBalance),
            PortfolioAtRisk = CalculatePortfolioAtRisk(loans),
            CashPosition = latestSummary?.CashBalance ?? 0,
            LastUpdateTime = latestSummary?.LastSyncDate ?? DateTime.MinValue,
            IsOnline = await CheckConnectivityAsync()
        };
    }

    public async Task<IEnumerable<OfflineLoan>> GetLoansAsync()
    {
        return await _context.Loans.OrderByDescending(l => l.DisbursementDate).ToListAsync();
    }

    public async Task<OfflineLoan?> GetLoanAsync(string loanId)
    {
        return await _context.Loans.FirstOrDefaultAsync(l => l.LoanId == loanId);
    }

    public async Task<IEnumerable<OfflineLoan>> GetOverdueLoansAsync()
    {
        return await _context.Loans.Where(l => l.DaysPastDue > 0).OrderByDescending(l => l.DaysPastDue).ToListAsync();
    }

    public async Task<IEnumerable<LoanSummary>> GetLoanSummariesAsync()
    {
        var loans = await _context.Loans.ToListAsync();
        return loans.Select(l => new LoanSummary
        {
            LoanId = l.LoanId,
            ClientName = l.ClientName,
            OutstandingBalance = l.OutstandingBalance,
            DaysPastDue = l.DaysPastDue,
            Status = l.Status,
            BoZClassification = l.BoZClassification,
            NextPaymentDate = CalculateNextPaymentDate(l),
            NextPaymentAmount = CalculateNextPaymentAmount(l)
        });
    }

    public async Task<IEnumerable<OfflineClient>> GetClientsAsync()
    {
        return await _context.Clients.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync();
    }

    public async Task<OfflineClient?> GetClientAsync(string clientId)
    {
        return await _context.Clients.FirstOrDefaultAsync(c => c.ClientId == clientId);
    }

    public async Task<IEnumerable<OfflinePayment>> GetPaymentsAsync()
    {
        return await _context.Payments.OrderByDescending(p => p.PaymentDate).ToListAsync();
    }

    public async Task<IEnumerable<OfflinePayment>> GetPaymentsByLoanAsync(string loanId)
    {
        return await _context.Payments.Where(p => p.LoanId == loanId).OrderByDescending(p => p.PaymentDate).ToListAsync();
    }

    public async Task<OfflinePayment?> GetPaymentAsync(string paymentId)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
    }

    public async Task<IEnumerable<OfflineFinancialSummary>> GetFinancialSummariesAsync()
    {
        return await _context.FinancialSummaries.OrderByDescending(f => f.SummaryDate).ToListAsync();
    }

    public async Task<OfflineFinancialSummary?> GetLatestFinancialSummaryAsync()
    {
        return await _context.FinancialSummaries.OrderByDescending(f => f.SummaryDate).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<OfflineReport>> GetReportsAsync()
    {
        return await _context.Reports.OrderByDescending(r => r.GeneratedDate).ToListAsync();
    }

    public async Task<OfflineReport?> GetReportAsync(string reportId)
    {
        return await _context.Reports.FirstOrDefaultAsync(r => r.ReportId == reportId);
    }

    public async Task<IEnumerable<OfflineSyncLog>> GetSyncLogsAsync()
    {
        return await _context.SyncLogs.OrderByDescending(s => s.Timestamp).Take(100).ToListAsync();
    }

    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        var lastSync = await _context.SyncLogs
            .Where(s => s.Status == "Success")
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();
        
        return lastSync?.Timestamp;
    }

    public async Task<bool> HasPendingSyncOperationsAsync()
    {
        return await _context.SyncLogs.AnyAsync(s => s.Status == "Pending");
    }

    public async Task SaveLoanAsync(OfflineLoan loan)
    {
        var existing = await _context.Loans.FirstOrDefaultAsync(l => l.LoanId == loan.LoanId);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(loan);
        }
        else
        {
            _context.Loans.Add(loan);
        }
        await _context.SaveChangesAsync();
    }

    public async Task SaveClientAsync(OfflineClient client)
    {
        var existing = await _context.Clients.FirstOrDefaultAsync(c => c.ClientId == client.ClientId);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(client);
        }
        else
        {
            _context.Clients.Add(client);
        }
        await _context.SaveChangesAsync();
    }

    public async Task SavePaymentAsync(OfflinePayment payment)
    {
        var existing = await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == payment.PaymentId);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(payment);
        }
        else
        {
            _context.Payments.Add(payment);
        }
        await _context.SaveChangesAsync();
    }

    public async Task SaveFinancialSummaryAsync(OfflineFinancialSummary summary)
    {
        _context.FinancialSummaries.Add(summary);
        await _context.SaveChangesAsync();
    }

    public async Task SaveReportAsync(OfflineReport report)
    {
        var existing = await _context.Reports.FirstOrDefaultAsync(r => r.ReportId == report.ReportId);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(report);
        }
        else
        {
            _context.Reports.Add(report);
        }
        await _context.SaveChangesAsync();
    }

    public async Task LogSyncOperationAsync(OfflineSyncLog syncLog)
    {
        _context.SyncLogs.Add(syncLog);
        await _context.SaveChangesAsync();
    }

    public async Task SaveLoansAsync(IEnumerable<OfflineLoan> loans)
    {
        foreach (var loan in loans)
        {
            await SaveLoanAsync(loan);
        }
    }

    public async Task SaveClientsAsync(IEnumerable<OfflineClient> clients)
    {
        foreach (var client in clients)
        {
            await SaveClientAsync(client);
        }
    }

    public async Task SavePaymentsAsync(IEnumerable<OfflinePayment> payments)
    {
        foreach (var payment in payments)
        {
            await SavePaymentAsync(payment);
        }
    }

    public async Task InitializeDatabaseAsync()
    {
        await _context.InitializeDatabaseAsync();
    }

    public async Task ClearAllDataAsync()
    {
        _context.Loans.RemoveRange(_context.Loans);
        _context.Clients.RemoveRange(_context.Clients);
        _context.Payments.RemoveRange(_context.Payments);
        _context.FinancialSummaries.RemoveRange(_context.FinancialSummaries);
        _context.Reports.RemoveRange(_context.Reports);
        _context.SyncLogs.RemoveRange(_context.SyncLogs);
        await _context.SaveChangesAsync();
    }

    public async Task<long> GetDatabaseSizeAsync()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "intellifin_offline.db");
        if (File.Exists(dbPath))
        {
            var fileInfo = new FileInfo(dbPath);
            return fileInfo.Length;
        }
        return 0;
    }

    private decimal CalculatePortfolioAtRisk(List<OfflineLoan> loans)
    {
        var totalPortfolio = loans.Sum(l => l.OutstandingBalance);
        var overdueAmount = loans.Where(l => l.DaysPastDue > 30).Sum(l => l.OutstandingBalance);
        return totalPortfolio > 0 ? (overdueAmount / totalPortfolio) * 100 : 0;
    }

    private DateTime CalculateNextPaymentDate(OfflineLoan loan)
    {
        // Simple calculation - in real implementation, this would be based on loan terms
        return DateTime.Today.AddDays(30);
    }

    private decimal CalculateNextPaymentAmount(OfflineLoan loan)
    {
        // Simple calculation - in real implementation, this would be based on loan terms
        return loan.OutstandingBalance * 0.1m; // 10% of outstanding balance
    }

    private async Task<bool> CheckConnectivityAsync()
    {
        // Simple connectivity check - in real implementation, this would ping the API
        return Connectivity.NetworkAccess == NetworkAccess.Internet;
    }
}
