using IntelliFin.FinancialService.Models;
using Hangfire;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// Main reporting service implementation with JasperReports integration
/// </summary>
public class ReportingService : IReportingService
{
    private readonly IJasperReportsClient _jasperClient;
    private readonly ILogger<ReportingService> _logger;
    private readonly IConfiguration _configuration;
    
    // In-memory storage for demo - in production would use database
    private static readonly List<ReportTemplate> _reportTemplates = new();
    private static readonly List<ScheduledReport> _scheduledReports = new();

    public ReportingService(
        IJasperReportsClient jasperClient,
        ILogger<ReportingService> logger,
        IConfiguration configuration)
    {
        _jasperClient = jasperClient;
        _logger = logger;
        _configuration = configuration;
        
        // Initialize default templates
        InitializeDefaultTemplates();
    }

    public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating report: {ReportType} in format: {Format}", request.ReportType, request.Format);

            // Find the report template
            var template = _reportTemplates.FirstOrDefault(t => t.TemplateId == request.ReportType);
            if (template == null)
            {
                throw new ArgumentException($"Report template not found: {request.ReportType}");
            }

            // Prepare parameters with default values
            var parameters = new Dictionary<string, object>(request.Parameters);
            
            // Add standard parameters
            parameters["GENERATED_BY"] = Environment.UserName;
            parameters["GENERATED_DATE"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            parameters["BRANCH_ID"] = request.BranchId ?? "ALL";
            
            if (request.StartDate.HasValue)
                parameters["START_DATE"] = request.StartDate.Value.ToString("yyyy-MM-dd");
            if (request.EndDate.HasValue)
                parameters["END_DATE"] = request.EndDate.Value.ToString("yyyy-MM-dd");

            // Execute the report via JasperReports
            var reportContent = await _jasperClient.ExecuteReportAsync(
                template.JasperReportPath, 
                parameters, 
                request.Format.ToLower());

            var response = new ReportResponse
            {
                ReportId = Guid.NewGuid().ToString(),
                FileName = GenerateFileName(template, request),
                ContentType = GetContentType(request.Format),
                Content = reportContent,
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed"
            };

            _logger.LogInformation("Successfully generated report: {ReportType}, Size: {Size} bytes", 
                request.ReportType, reportContent.Length);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report: {ReportType}", request.ReportType);
            throw;
        }
    }

    public async Task<List<ReportTemplate>> GetReportTemplatesAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving available report templates");
            
            // In production, this would query from database
            await Task.Delay(10); // Simulate async operation
            
            return _reportTemplates.Where(t => t.IsActive).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report templates");
            throw;
        }
    }

    public async Task<ReportTemplate?> GetReportTemplateAsync(string templateId)
    {
        try
        {
            _logger.LogInformation("Retrieving report template: {TemplateId}", templateId);
            
            await Task.Delay(10); // Simulate async operation
            
            return _reportTemplates.FirstOrDefault(t => t.TemplateId == templateId && t.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<string> ScheduleReportAsync(ScheduledReport scheduledReport)
    {
        try
        {
            _logger.LogInformation("Scheduling report: {ReportType} with cron: {CronExpression}", 
                scheduledReport.ReportType, scheduledReport.CronExpression);

            scheduledReport.ScheduleId = Guid.NewGuid().ToString();
            scheduledReport.NextRunTime = CalculateNextRunTime(scheduledReport.CronExpression);
            
            // Store the scheduled report
            _scheduledReports.Add(scheduledReport);

            // Schedule with Hangfire
            RecurringJob.AddOrUpdate(
                scheduledReport.ScheduleId,
                () => ExecuteScheduledReportAsync(scheduledReport.ScheduleId),
                scheduledReport.CronExpression);

            await Task.CompletedTask; // Satisfy async requirement

            _logger.LogInformation("Successfully scheduled report: {ScheduleId}", scheduledReport.ScheduleId);
            return scheduledReport.ScheduleId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling report: {ReportType}", scheduledReport.ReportType);
            throw;
        }
    }

    public async Task<List<ScheduledReport>> GetScheduledReportsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving scheduled reports");
            
            await Task.Delay(10); // Simulate async operation
            
            return _scheduledReports.Where(s => s.IsActive).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled reports");
            throw;
        }
    }

    public async Task<bool> CancelScheduledReportAsync(string scheduleId)
    {
        try
        {
            _logger.LogInformation("Cancelling scheduled report: {ScheduleId}", scheduleId);

            var scheduledReport = _scheduledReports.FirstOrDefault(s => s.ScheduleId == scheduleId);
            if (scheduledReport != null)
            {
                scheduledReport.IsActive = false;
                RecurringJob.RemoveIfExists(scheduleId);
                
                await Task.CompletedTask; // Satisfy async requirement
                
                _logger.LogInformation("Successfully cancelled scheduled report: {ScheduleId}", scheduleId);
                return true;
            }

            _logger.LogWarning("Scheduled report not found: {ScheduleId}", scheduleId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling scheduled report: {ScheduleId}", scheduleId);
            return false;
        }
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledReportAsync(string scheduleId)
    {
        try
        {
            _logger.LogInformation("Executing scheduled report: {ScheduleId}", scheduleId);

            var scheduledReport = _scheduledReports.FirstOrDefault(s => s.ScheduleId == scheduleId && s.IsActive);
            if (scheduledReport == null)
            {
                _logger.LogWarning("Scheduled report not found or inactive: {ScheduleId}", scheduleId);
                return;
            }

            // Create report request from scheduled report
            var reportRequest = new ReportRequest
            {
                ReportType = scheduledReport.ReportType,
                Parameters = scheduledReport.Parameters,
                Format = scheduledReport.Format
            };

            // Generate the report
            var reportResponse = await GenerateReportAsync(reportRequest);

            // Update last run time
            scheduledReport.LastRunTime = DateTime.UtcNow;
            scheduledReport.NextRunTime = CalculateNextRunTime(scheduledReport.CronExpression);

            // In production, would:
            // 1. Save report to storage
            // 2. Send to recipients via email/notification service
            // 3. Log execution results
            
            _logger.LogInformation("Successfully executed scheduled report: {ScheduleId}, Generated: {FileName}", 
                scheduleId, reportResponse.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled report: {ScheduleId}", scheduleId);
            throw; // Let Hangfire handle retry logic
        }
    }

    private void InitializeDefaultTemplates()
    {
        var templates = new List<ReportTemplate>
        {
            new()
            {
                TemplateId = "BOZ_NPL_CLASSIFICATION",
                Name = "BoZ NPL Classification Report",
                Description = "Bank of Zambia Non-Performing Loans Classification Report",
                Category = "Regulatory",
                JasperReportPath = "/reports/boz/npl_classification",
                Parameters = new List<ReportParameter>
                {
                    new() { Name = "AS_OF_DATE", DisplayName = "As of Date", DataType = "Date", Required = true },
                    new() { Name = "BRANCH_ID", DisplayName = "Branch", DataType = "String", Required = false, DefaultValue = "ALL" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                TemplateId = "BOZ_CAPITAL_ADEQUACY",
                Name = "BoZ Capital Adequacy Report",
                Description = "Bank of Zambia Capital Adequacy Ratio Report",
                Category = "Regulatory",
                JasperReportPath = "/reports/boz/capital_adequacy",
                Parameters = new List<ReportParameter>
                {
                    new() { Name = "AS_OF_DATE", DisplayName = "As of Date", DataType = "Date", Required = true },
                    new() { Name = "BRANCH_ID", DisplayName = "Branch", DataType = "String", Required = false, DefaultValue = "ALL" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                TemplateId = "LOAN_PORTFOLIO_SUMMARY",
                Name = "Loan Portfolio Summary",
                Description = "Comprehensive loan portfolio summary report",
                Category = "Portfolio Management",
                JasperReportPath = "/reports/boz/loan_portfolio_summary",
                Parameters = new List<ReportParameter>
                {
                    new() { Name = "START_DATE", DisplayName = "Start Date", DataType = "Date", Required = true },
                    new() { Name = "END_DATE", DisplayName = "End Date", DataType = "Date", Required = true },
                    new() { Name = "BRANCH_ID", DisplayName = "Branch", DataType = "String", Required = false, DefaultValue = "ALL" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _reportTemplates.AddRange(templates);
    }

    private static string GenerateFileName(ReportTemplate template, ReportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var branchSuffix = string.IsNullOrEmpty(request.BranchId) ? "ALL" : request.BranchId;
        var extension = request.Format.ToLower() switch
        {
            "pdf" => "pdf",
            "excel" => "xlsx",
            "csv" => "csv",
            _ => "pdf"
        };

        return $"{template.Name.Replace(" ", "_")}_{branchSuffix}_{timestamp}.{extension}";
    }

    private static string GetContentType(string format) => format.ToLower() switch
    {
        "pdf" => "application/pdf",
        "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "csv" => "text/csv",
        _ => "application/pdf"
    };

    private static DateTime CalculateNextRunTime(string cronExpression)
    {
        // Simplified cron calculation - in production would use proper cron library
        return DateTime.UtcNow.AddHours(24); // Default to daily
    }
}