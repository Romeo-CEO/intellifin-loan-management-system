using System.ComponentModel.DataAnnotations;

namespace IntelliFin.FinancialService.Models;

#region Payment Optimization Request/Response Models

/// <summary>
/// Request model for payment optimization analysis
/// </summary>
public class PaymentOptimizationRequest
{
    [Required]
    public string PaymentId { get; set; } = string.Empty;
    
    [Required] 
    public string Gateway { get; set; } = string.Empty;
    
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
    
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    
    public string? BranchId { get; set; }
    
    public List<string> IncludeMetrics { get; set; } = new();
}

/// <summary>
/// Payment optimization analysis response
/// </summary>
public class PaymentOptimizationResponse
{
    public string OptimizationId { get; set; } = string.Empty;
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
    
    // Performance Metrics
    public double OverallSuccessRate { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public double ThroughputPerMinute { get; set; }
    
    // Gateway Performance
    public Dictionary<string, GatewayOptimizationMetrics> GatewayMetrics { get; set; } = new();
    
    // Optimization Recommendations
    public List<OptimizationRecommendation> Recommendations { get; set; } = new();
    
    // Performance Trends
    public PaymentPerformanceTrends Trends { get; set; } = new();
    
    // Risk Assessment
    public PaymentRiskAssessment RiskAssessment { get; set; } = new();
}

#endregion

#region Gateway Optimization Models

/// <summary>
/// Gateway-specific optimization metrics
/// </summary>
public class GatewayOptimizationMetrics
{
    public string GatewayName { get; set; } = string.Empty;
    
    // Performance Metrics
    public double SuccessRate { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan P95ResponseTime { get; set; }
    public TimeSpan P99ResponseTime { get; set; }
    
    // Volume Metrics
    public int TotalTransactions { get; set; }
    public double TransactionsPerHour { get; set; }
    public decimal TotalVolume { get; set; }
    
    // Reliability Metrics
    public double UptimePercentage { get; set; }
    public int TimeoutCount { get; set; }
    public int RetryCount { get; set; }
    
    // Error Analysis
    public Dictionary<string, int> ErrorBreakdown { get; set; } = new();
    public List<string> CommonFailureReasons { get; set; } = new();
    
    // Cost Metrics
    public decimal ProcessingCostPerTransaction { get; set; }
    public decimal TotalProcessingCost { get; set; }
    
    // Quality Score (0-100)
    public double QualityScore { get; set; }
}

#endregion

#region Optimization Recommendations

/// <summary>
/// Optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string RecommendationId { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    
    // Impact Assessment
    public OptimizationImpact ExpectedImpact { get; set; } = new();
    
    // Implementation Details
    public List<string> ImplementationSteps { get; set; } = new();
    public TimeSpan EstimatedImplementationTime { get; set; }
    public string? ConfigurationChanges { get; set; }
    
    // Metrics
    public Dictionary<string, object> SupportingMetrics { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsImplemented { get; set; }
    public DateTime? ImplementedAt { get; set; }
}

/// <summary>
/// Expected impact of optimization
/// </summary>
public class OptimizationImpact
{
    public double SuccessRateImprovement { get; set; } // Percentage points
    public TimeSpan ResponseTimeImprovement { get; set; }
    public double ThroughputImprovement { get; set; } // Percentage
    public decimal CostSavingsPerMonth { get; set; }
    public double RiskReduction { get; set; } // Percentage
    public string ConfidenceLevel { get; set; } = "Medium"; // Low, Medium, High
}

#endregion

#region Performance Trends

/// <summary>
/// Payment performance trends analysis
/// </summary>
public class PaymentPerformanceTrends
{
    // Time-based trends
    public Dictionary<DateTime, double> SuccessRateByDay { get; set; } = new();
    public Dictionary<DateTime, TimeSpan> ResponseTimeByDay { get; set; } = new();
    public Dictionary<DateTime, int> VolumeByDay { get; set; } = new();
    
    // Hourly patterns
    public Dictionary<int, double> SuccessRateByHour { get; set; } = new();
    public Dictionary<int, TimeSpan> ResponseTimeByHour { get; set; } = new();
    public Dictionary<int, int> VolumeByHour { get; set; } = new();
    
    // Trends by gateway
    public Dictionary<string, Dictionary<DateTime, double>> GatewayTrends { get; set; } = new();
    
    // Performance indicators
    public TrendDirection OverallTrend { get; set; }
    public List<string> PerformanceInsights { get; set; } = new();
    public List<AnomalyDetection> DetectedAnomalies { get; set; } = new();
}

/// <summary>
/// Anomaly detection result
/// </summary>
public class AnomalyDetection
{
    public string AnomalyId { get; set; } = string.Empty;
    public AnomalyType Type { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime AnomalyPeriodStart { get; set; }
    public DateTime AnomalyPeriodEnd { get; set; }
    
    public string Description { get; set; } = string.Empty;
    public double SeverityScore { get; set; } // 0-10 scale
    public string AffectedGateway { get; set; } = string.Empty;
    
    public Dictionary<string, object> MetricValues { get; set; } = new();
    public List<string> PotentialCauses { get; set; } = new();
}

#endregion

#region Risk Assessment

/// <summary>
/// Payment risk assessment
/// </summary>
public class PaymentRiskAssessment
{
    public double OverallRiskScore { get; set; } // 0-100 scale
    public RiskLevel RiskLevel { get; set; }
    
    // Risk Categories
    public Dictionary<RiskCategory, RiskCategoryAssessment> RiskCategories { get; set; } = new();
    
    // Risk Factors
    public List<RiskFactor> IdentifiedRisks { get; set; } = new();
    
    // Mitigation Strategies
    public List<RiskMitigationStrategy> MitigationStrategies { get; set; } = new();
    
    public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;
    public string AssessedBy { get; set; } = "System";
}

/// <summary>
/// Risk category assessment
/// </summary>
public class RiskCategoryAssessment
{
    public RiskCategory Category { get; set; }
    public double RiskScore { get; set; } // 0-100
    public RiskLevel Level { get; set; }
    public List<string> RiskIndicators { get; set; } = new();
    public string Assessment { get; set; } = string.Empty;
}

/// <summary>
/// Individual risk factor
/// </summary>
public class RiskFactor
{
    public string FactorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Impact { get; set; } // 0-10 scale
    public double Probability { get; set; } // 0-10 scale
    public double RiskScore { get; set; } // Impact * Probability
    public RiskLevel Level { get; set; }
    public List<string> Indicators { get; set; } = new();
}

/// <summary>
/// Risk mitigation strategy
/// </summary>
public class RiskMitigationStrategy
{
    public string StrategyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RiskCategory> AddressedRiskCategories { get; set; } = new();
    public double EffectivenessScore { get; set; } // 0-100
    public List<string> ImplementationSteps { get; set; } = new();
    public TimeSpan ImplementationTime { get; set; }
    public decimal ImplementationCost { get; set; }
}

#endregion

#region Enums

public enum RecommendationType
{
    PerformanceOptimization,
    CostOptimization,
    ReliabilityImprovement,
    SecurityEnhancement,
    ConfigurationTuning,
    InfrastructureScaling,
    ProcessImprovement
}

public enum RecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TrendDirection
{
    Improving,
    Stable,
    Degrading,
    Volatile,
    Unknown
}

public enum AnomalyType
{
    SuccessRateDropped,
    ResponseTimeSpike,
    VolumeAnomaly,
    ErrorRateIncrease,
    GatewayOutage,
    PerformanceDegradation
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RiskCategory
{
    OperationalRisk,
    FinancialRisk,
    TechnicalRisk,
    SecurityRisk,
    ComplianceRisk,
    ReputationalRisk
}

#endregion

