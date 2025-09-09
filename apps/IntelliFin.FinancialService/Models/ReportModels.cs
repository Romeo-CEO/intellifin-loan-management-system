using System.ComponentModel.DataAnnotations;

namespace IntelliFin.FinancialService.Models;

#region Report Request/Response Models

/// <summary>
/// Report generation request model
/// </summary>
public class ReportRequest
{
    [Required]
    public string ReportType { get; set; } = string.Empty;
    
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    [Required]
    public string Format { get; set; } = "PDF"; // PDF, Excel, CSV
    
    public string? BranchId { get; set; }
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Report generation response model
/// </summary>
public class ReportResponse
{
    public string ReportId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime GeneratedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

#endregion

#region Report Template Models

/// <summary>
/// Report template definition
/// </summary>
public class ReportTemplate
{
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string JasperReportPath { get; set; } = string.Empty;
    public List<ReportParameter> Parameters { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Report parameter definition
/// </summary>
public class ReportParameter
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty; // String, Date, Number, Boolean
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public List<string>? ValidValues { get; set; }
}

#endregion

#region Scheduled Report Models

/// <summary>
/// Scheduled report configuration
/// </summary>
public class ScheduledReport
{
    public string ScheduleId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string Format { get; set; } = "PDF";
    public string CronExpression { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

#endregion

#region Dashboard Models

/// <summary>
/// Real-time dashboard metrics
/// </summary>
public class DashboardMetrics
{
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
    public string? BranchId { get; set; }
    
    // Financial Metrics
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal NetWorth { get; set; }
    public decimal CashPosition { get; set; }
    
    // Loan Portfolio Metrics  
    public int ActiveLoans { get; set; }
    public decimal TotalLoanBalance { get; set; }
    public decimal AverrageLoanSize { get; set; }
    public double PortfolioAtRisk { get; set; } // PAR percentage
    
    // Performance Metrics
    public decimal MonthlyDisbursements { get; set; }
    public decimal MonthlyCollections { get; set; }
    public decimal MonthlyInterestIncome { get; set; }
    public decimal MonthlyOperatingExpenses { get; set; }
    
    // Risk Metrics
    public double NplRatio { get; set; } // Non-performing loan ratio
    public decimal ProvisionCoverage { get; set; }
    public double CapitalAdequacyRatio { get; set; }
    
    // Operational Metrics
    public int NewClientsThisMonth { get; set; }
    public int LoanApplicationsPending { get; set; }
    public double AverageProcessingTime { get; set; } // In days
    
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

#endregion

#region BoZ Regulatory Models

/// <summary>
/// Bank of Zambia NPL Classification Report
/// </summary>
public class BozNplClassificationReport
{
    public DateTime AsOfDate { get; set; }
    public string BranchId { get; set; } = string.Empty;
    
    public List<NplClassificationEntry> Classifications { get; set; } = new();
    
    public decimal TotalOutstanding { get; set; }
    public decimal TotalNplAmount { get; set; }
    public double NplRatio { get; set; }
    public decimal ProvisionRequired { get; set; }
    public decimal ProvisionCoverage { get; set; }
}

public class NplClassificationEntry
{
    public string Classification { get; set; } = string.Empty; // Current, Special Mention, Substandard, Doubtful, Loss
    public int DaysOverdue { get; set; }
    public int NumberOfAccounts { get; set; }
    public decimal Outstanding { get; set; }
    public decimal ProvisionRate { get; set; }
    public decimal ProvisionAmount { get; set; }
}

/// <summary>
/// Bank of Zambia Capital Adequacy Report
/// </summary>
public class BozCapitalAdequacyReport
{
    public DateTime AsOfDate { get; set; }
    public string BranchId { get; set; } = string.Empty;
    
    // Capital Components
    public decimal Tier1Capital { get; set; }
    public decimal Tier2Capital { get; set; }
    public decimal TotalCapital { get; set; }
    
    // Risk-Weighted Assets
    public decimal CreditRiskWeightedAssets { get; set; }
    public decimal OperationalRiskWeightedAssets { get; set; }
    public decimal MarketRiskWeightedAssets { get; set; }
    public decimal TotalRiskWeightedAssets { get; set; }
    
    // Ratios
    public double Tier1CapitalRatio { get; set; }
    public double TotalCapitalRatio { get; set; }
    public bool IsCompliant { get; set; }
    
    public List<CapitalAdequacyBreakdown> Breakdown { get; set; } = new();
}

public class CapitalAdequacyBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal RiskWeight { get; set; }
    public decimal RiskWeightedAmount { get; set; }
}

/// <summary>
/// Bank of Zambia Prudential Report
/// </summary>
public class BozPrudentialReport
{
    public DateTime ReportingPeriod { get; set; }
    public string BranchId { get; set; } = string.Empty;
    
    // Key Prudential Ratios
    public double CapitalAdequacyRatio { get; set; }
    public double Tier1CapitalRatio { get; set; }
    public double LiquidityRatio { get; set; }
    public double NplRatio { get; set; }
    public double ProvisionCoverage { get; set; }
    public double LargeExposureRatio { get; set; }
    
    // Regulatory Limits
    public Dictionary<string, RegulatoryLimit> RegulatoryLimits { get; set; } = new();
    
    // Compliance Status
    public List<ComplianceIssue> ComplianceIssues { get; set; } = new();
    
    public bool OverallCompliance { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class RegulatoryLimit
{
    public string Name { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal LimitValue { get; set; }
    public bool IsCompliant { get; set; }
    public string Unit { get; set; } = string.Empty; // Percentage, Amount, etc.
}

public class ComplianceIssue
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // High, Medium, Low
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime IdentifiedDate { get; set; }
}

#endregion

