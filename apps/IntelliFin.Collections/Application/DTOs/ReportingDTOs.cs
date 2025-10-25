namespace IntelliFin.Collections.Application.DTOs;

public record AgingAnalysisReport
{
    public DateTime AsOfDate { get; init; }
    public List<AgingBucket> AgingBuckets { get; init; } = new();
    public decimal TotalOutstanding { get; init; }
    public int TotalLoans { get; init; }
}

public record AgingBucket
{
    public string BucketName { get; init; } = string.Empty;
    public int MinDays { get; init; }
    public int? MaxDays { get; init; }
    public int LoanCount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public decimal PercentageOfTotal { get; init; }
}

public record PortfolioAtRiskReport
{
    public DateTime AsOfDate { get; init; }
    public decimal TotalPortfolioBalance { get; init; }
    public decimal Par30Amount { get; init; }
    public decimal Par30Rate { get; init; }
    public decimal Par60Amount { get; init; }
    public decimal Par60Rate { get; init; }
    public decimal Par90Amount { get; init; }
    public decimal Par90Rate { get; init; }
    public int TotalLoans { get; init; }
    public int LoansInArrears { get; init; }
}

public record ProvisioningReport
{
    public DateTime AsOfDate { get; init; }
    public List<ProvisioningByClassification> ByClassification { get; init; } = new();
    public decimal TotalOutstanding { get; init; }
    public decimal TotalProvisionRequired { get; init; }
    public decimal ProvisionCoverageRatio { get; init; }
}

public record ProvisioningByClassification
{
    public string Classification { get; init; } = string.Empty;
    public int LoanCount { get; init; }
    public decimal OutstandingBalance { get; init; }
    public decimal ProvisionRate { get; init; }
    public decimal ProvisionAmount { get; init; }
}

public record RecoveryAnalyticsReport
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal TotalCollected { get; init; }
    public decimal PrincipalCollected { get; init; }
    public decimal InterestCollected { get; init; }
    public int PaymentsReceived { get; init; }
    public decimal AveragePaymentSize { get; init; }
    public List<CollectionsByMethod> ByPaymentMethod { get; init; } = new();
    public decimal RecoveryRate { get; init; }
}

public record CollectionsByMethod
{
    public string PaymentMethod { get; init; } = string.Empty;
    public int PaymentCount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PercentageOfTotal { get; init; }
}

public record CollectionsDashboard
{
    public DateTime GeneratedAt { get; init; }
    
    // Portfolio Overview
    public int TotalActiveLoans { get; init; }
    public decimal TotalOutstanding { get; init; }
    public decimal TotalInArrears { get; init; }
    public decimal ArrearsRate { get; init; }
    
    // Collections Performance (MTD)
    public decimal CollectionsThisMonth { get; init; }
    public decimal CollectionsTarget { get; init; }
    public decimal CollectionsAchievement { get; init; }
    
    // Portfolio Quality
    public decimal Par30Rate { get; init; }
    public decimal ProvisionCoverageRatio { get; init; }
    
    // Classification Breakdown
    public Dictionary<string, int> LoansByClassification { get; init; } = new();
    
    // Top Delinquent Accounts
    public List<DelinquentLoanSummary> TopDelinquentLoans { get; init; } = new();
}

public record DelinquentLoanSummary
{
    public Guid LoanId { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public decimal OutstandingBalance { get; init; }
    public int DaysPastDue { get; init; }
    public string Classification { get; init; } = string.Empty;
}
