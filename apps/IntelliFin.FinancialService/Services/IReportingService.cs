using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// Interface for reporting service operations
/// </summary>
public interface IReportingService
{
    /// <summary>
    /// Generate a report using JasperReports
    /// </summary>
    Task<ReportResponse> GenerateReportAsync(ReportRequest request);

    /// <summary>
    /// Get available report templates
    /// </summary>
    Task<List<ReportTemplate>> GetReportTemplatesAsync();

    /// <summary>
    /// Get report template by ID
    /// </summary>
    Task<ReportTemplate?> GetReportTemplateAsync(string templateId);

    /// <summary>
    /// Schedule a report for automated generation
    /// </summary>
    Task<string> ScheduleReportAsync(ScheduledReport scheduledReport);

    /// <summary>
    /// Get scheduled reports
    /// </summary>
    Task<List<ScheduledReport>> GetScheduledReportsAsync();

    /// <summary>
    /// Cancel a scheduled report
    /// </summary>
    Task<bool> CancelScheduledReportAsync(string scheduleId);
}

/// <summary>
/// Interface for BoZ regulatory reporting
/// </summary>
public interface IBozReportingService
{
    /// <summary>
    /// Generate BoZ prudential report
    /// </summary>
    Task<BozPrudentialReport> GeneratePrudentialReportAsync(DateTime reportingPeriod, string branchId);

    /// <summary>
    /// Get NPL classification report
    /// </summary>
    Task<ReportResponse> GenerateNplClassificationReportAsync(DateTime asOfDate, string branchId);

    /// <summary>
    /// Generate Capital Adequacy Ratio report
    /// </summary>
    Task<ReportResponse> GenerateCapitalAdequacyReportAsync(DateTime asOfDate, string branchId);

    /// <summary>
    /// Generate loan portfolio summary
    /// </summary>
    Task<ReportResponse> GenerateLoanPortfolioSummaryAsync(DateTime startDate, DateTime endDate, string branchId);

    /// <summary>
    /// Submit report to BoZ (automated submission)
    /// </summary>
    Task<bool> SubmitReportToBozAsync(string reportId);
}

/// <summary>
/// Interface for dashboard and real-time reporting
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Get real-time dashboard metrics
    /// </summary>
    Task<DashboardMetrics> GetDashboardMetricsAsync(string? branchId = null);

    /// <summary>
    /// Get GL account balances for dashboard
    /// </summary>
    Task<Dictionary<string, decimal>> GetAccountBalanceSummaryAsync(string? branchId = null);

    /// <summary>
    /// Get loan performance metrics
    /// </summary>
    Task<Dictionary<string, object>> GetLoanPerformanceMetricsAsync(string? branchId = null);

    /// <summary>
    /// Get cash flow metrics
    /// </summary>
    Task<Dictionary<string, decimal>> GetCashFlowMetricsAsync(DateTime startDate, DateTime endDate, string? branchId = null);

    /// <summary>
    /// Get collection performance metrics
    /// </summary>
    Task<Dictionary<string, object>> GetCollectionMetricsAsync(string? branchId = null);
}

/// <summary>
/// Interface for JasperReports Server integration
/// </summary>
public interface IJasperReportsClient
{
    /// <summary>
    /// Execute a JasperReports report
    /// </summary>
    Task<byte[]> ExecuteReportAsync(string reportPath, Dictionary<string, object> parameters, string format = "pdf");

    /// <summary>
    /// Get available reports from JasperReports Server
    /// </summary>
    Task<List<string>> GetAvailableReportsAsync();

    /// <summary>
    /// Upload a report template to JasperReports Server
    /// </summary>
    Task<bool> UploadReportTemplateAsync(string reportPath, byte[] jrxmlContent);

    /// <summary>
    /// Test connection to JasperReports Server
    /// </summary>
    Task<bool> TestConnectionAsync();
}