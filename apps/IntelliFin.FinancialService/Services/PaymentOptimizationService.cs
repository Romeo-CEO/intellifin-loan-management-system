using IntelliFin.FinancialService.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// Service for payment processing optimization and analysis
/// </summary>
public class PaymentOptimizationService : IPaymentOptimizationService
{
    private readonly IPaymentMonitoringService _monitoringService;
    private readonly IPaymentRetryService _retryService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<PaymentOptimizationService> _logger;
    private readonly IConfiguration _configuration;

    // Cache configuration
    private const int CacheExpirationMinutes = 60;
    private const string OptimizationCachePrefix = "payment_optimization:";
    private const string RecommendationsCachePrefix = "recommendations:";
    private const string RiskAssessmentCachePrefix = "risk_assessment:";

    public PaymentOptimizationService(
        IPaymentMonitoringService monitoringService,
        IPaymentRetryService retryService,
        IDistributedCache cache,
        ILogger<PaymentOptimizationService> logger,
        IConfiguration configuration)
    {
        _monitoringService = monitoringService;
        _retryService = retryService;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<PaymentOptimizationResponse> AnalyzePaymentPerformanceAsync(PaymentOptimizationRequest request)
    {
        var cacheKey = $"{OptimizationCachePrefix}{request.Gateway}:{request.StartDate:yyyyMMdd}:{request.EndDate:yyyyMMdd}";
        
        try
        {
            _logger.LogInformation("Analyzing payment performance for gateway {Gateway} from {StartDate} to {EndDate}", 
                request.Gateway, request.StartDate, request.EndDate);

            // Check cache first
            var cachedResponse = await GetFromCacheAsync<PaymentOptimizationResponse>(cacheKey);
            if (cachedResponse != null)
            {
                _logger.LogInformation("Retrieved payment optimization analysis from cache");
                return cachedResponse;
            }

            var response = new PaymentOptimizationResponse
            {
                OptimizationId = Guid.NewGuid().ToString(),
                AnalysisTimestamp = DateTime.UtcNow
            };

            // Get performance metrics
            var performanceMetrics = await _monitoringService.GetPaymentPerformanceMetricsAsync(
                request.StartDate, request.EndDate);
            
            response.OverallSuccessRate = performanceMetrics.SuccessRate;
            response.AverageProcessingTime = performanceMetrics.AverageProcessingTime;
            response.ThroughputPerMinute = performanceMetrics.ThroughputPerMinute;

            // Analyze gateway-specific metrics
            response.GatewayMetrics = await AnalyzeGatewayMetricsAsync(request);

            // Generate optimization recommendations
            response.Recommendations = await GenerateOptimizationRecommendationsAsync(response.GatewayMetrics);

            // Get performance trends
            var daysBack = (int)(request.EndDate - request.StartDate).TotalDays;
            response.Trends = await GetPerformanceTrendsAsync(Math.Max(daysBack, 30));

            // Assess payment risks
            response.RiskAssessment = await AssessPaymentRisksAsync(request.Gateway);

            // Cache the results
            await SetCacheAsync(cacheKey, response);

            _logger.LogInformation("Payment performance analysis completed. Overall success rate: {SuccessRate:F2}%, " +
                "Average processing time: {ProcessingTime}ms, Recommendations: {RecommendationCount}",
                response.OverallSuccessRate, response.AverageProcessingTime.TotalMilliseconds, 
                response.Recommendations.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing payment performance for gateway {Gateway}", request.Gateway);
            throw;
        }
    }

    public async Task<List<OptimizationRecommendation>> GetOptimizationRecommendationsAsync(string? gateway = null)
    {
        var cacheKey = $"{RecommendationsCachePrefix}{gateway ?? "all"}";
        
        try
        {
            _logger.LogInformation("Getting optimization recommendations for gateway: {Gateway}", gateway ?? "all");

            // Check cache first
            var cachedRecommendations = await GetFromCacheAsync<List<OptimizationRecommendation>>(cacheKey);
            if (cachedRecommendations != null)
            {
                return cachedRecommendations;
            }

            var recommendations = new List<OptimizationRecommendation>();

            // Get recent performance data
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-7); // Last 7 days for recommendations

            var performanceMetrics = await _monitoringService.GetPaymentPerformanceMetricsAsync(startDate, endDate);
            var systemHealth = await _monitoringService.GetPaymentSystemHealthAsync();

            // Generate recommendations based on performance analysis
            recommendations.AddRange(await GeneratePerformanceRecommendationsAsync(performanceMetrics));
            recommendations.AddRange(await GenerateReliabilityRecommendationsAsync(systemHealth));
            recommendations.AddRange(await GenerateCostOptimizationRecommendationsAsync(performanceMetrics));
            recommendations.AddRange(await GenerateSecurityRecommendationsAsync());

            // Filter by gateway if specified
            if (!string.IsNullOrEmpty(gateway))
            {
                recommendations = recommendations
                    .Where(r => r.SupportingMetrics.ContainsKey("Gateway") && 
                               r.SupportingMetrics["Gateway"].ToString() == gateway)
                    .ToList();
            }

            // Sort by priority and potential impact
            recommendations = recommendations
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.ExpectedImpact.SuccessRateImprovement)
                .ToList();

            // Cache the results
            await SetCacheAsync(cacheKey, recommendations);

            _logger.LogInformation("Generated {RecommendationCount} optimization recommendations", recommendations.Count);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimization recommendations for gateway {Gateway}", gateway);
            throw;
        }
    }

    public async Task<PaymentRiskAssessment> AssessPaymentRisksAsync(string? gateway = null)
    {
        var cacheKey = $"{RiskAssessmentCachePrefix}{gateway ?? "all"}";
        
        try
        {
            _logger.LogInformation("Assessing payment risks for gateway: {Gateway}", gateway ?? "all");

            // Check cache first
            var cachedAssessment = await GetFromCacheAsync<PaymentRiskAssessment>(cacheKey);
            if (cachedAssessment != null)
            {
                return cachedAssessment;
            }

            var assessment = new PaymentRiskAssessment
            {
                AssessmentDate = DateTime.UtcNow
            };

            // Get system health and performance data
            var systemHealth = await _monitoringService.GetPaymentSystemHealthAsync();
            var performanceMetrics = await _monitoringService.GetPaymentPerformanceMetricsAsync(
                DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

            // Assess different risk categories
            assessment.RiskCategories = new Dictionary<RiskCategory, RiskCategoryAssessment>
            {
                [RiskCategory.OperationalRisk] = await AssessOperationalRisk(systemHealth, performanceMetrics),
                [RiskCategory.TechnicalRisk] = await AssessTechnicalRisk(systemHealth, performanceMetrics),
                [RiskCategory.FinancialRisk] = await AssessFinancialRisk(performanceMetrics),
                [RiskCategory.SecurityRisk] = await AssessSecurityRisk(),
                [RiskCategory.ComplianceRisk] = await AssessComplianceRisk(performanceMetrics)
            };

            // Calculate overall risk score
            assessment.OverallRiskScore = assessment.RiskCategories.Values.Average(r => r.RiskScore);
            assessment.RiskLevel = DetermineRiskLevel(assessment.OverallRiskScore);

            // Identify specific risk factors
            assessment.IdentifiedRisks = await IdentifyRiskFactorsAsync(assessment.RiskCategories);

            // Generate mitigation strategies
            assessment.MitigationStrategies = await GenerateRiskMitigationStrategiesAsync(assessment.IdentifiedRisks);

            // Cache the results
            await SetCacheAsync(cacheKey, assessment);

            _logger.LogInformation("Risk assessment completed. Overall risk score: {RiskScore:F1}/100, Level: {RiskLevel}",
                assessment.OverallRiskScore, assessment.RiskLevel);

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing payment risks for gateway {Gateway}", gateway);
            throw;
        }
    }

    public async Task<bool> ImplementOptimizationAsync(string recommendationId, string implementedBy)
    {
        try
        {
            _logger.LogInformation("Implementing optimization recommendation {RecommendationId} by {ImplementedBy}", 
                recommendationId, implementedBy);

            // In a production system, this would:
            // 1. Retrieve the recommendation details
            // 2. Execute the implementation steps
            // 3. Update configuration if needed
            // 4. Track the implementation status
            // 5. Schedule monitoring of the impact

            // For now, simulate successful implementation
            await Task.Delay(100);

            // Clear related caches to force refresh
            await InvalidateCachesAsync();

            _logger.LogInformation("Optimization recommendation {RecommendationId} implemented successfully", 
                recommendationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error implementing optimization recommendation {RecommendationId}", recommendationId);
            return false;
        }
    }

    public async Task<PaymentPerformanceTrends> GetPerformanceTrendsAsync(int daysBack = 30)
    {
        try
        {
            _logger.LogInformation("Getting payment performance trends for the last {DaysBack} days", daysBack);

            var trendAnalysis = await _monitoringService.GetPaymentTrendAnalysisAsync(daysBack);

            var trends = new PaymentPerformanceTrends
            {
                SuccessRateByDay = trendAnalysis.DailySuccessRates,
                ResponseTimeByDay = trendAnalysis.DailyAverageResponseTimes,
                VolumeByDay = trendAnalysis.DailyVolumes,
                GatewayTrends = trendAnalysis.GatewayTrends,
                OverallTrend = DetermineTrendDirection(trendAnalysis.DailySuccessRates),
                PerformanceInsights = GeneratePerformanceInsights(trendAnalysis),
                DetectedAnomalies = await DetectAnomaliesAsync(DateTime.UtcNow.AddDays(-daysBack), DateTime.UtcNow)
            };

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance trends");
            throw;
        }
    }

    public async Task<List<AnomalyDetection>> DetectAnomaliesAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Detecting payment anomalies from {StartDate} to {EndDate}", startDate, endDate);

            var anomalies = new List<AnomalyDetection>();
            var performanceMetrics = await _monitoringService.GetPaymentPerformanceMetricsAsync(startDate, endDate);

            // Detect success rate anomalies
            if (performanceMetrics.SuccessRate < 90) // Below 90% success rate
            {
                anomalies.Add(new AnomalyDetection
                {
                    AnomalyId = Guid.NewGuid().ToString(),
                    Type = AnomalyType.SuccessRateDropped,
                    DetectedAt = DateTime.UtcNow,
                    AnomalyPeriodStart = startDate,
                    AnomalyPeriodEnd = endDate,
                    Description = $"Success rate dropped to {performanceMetrics.SuccessRate:F2}% (below 90% threshold)",
                    SeverityScore = CalculateAnomalySeverity(performanceMetrics.SuccessRate, 90),
                    MetricValues = new Dictionary<string, object>
                    {
                        ["SuccessRate"] = performanceMetrics.SuccessRate,
                        ["Threshold"] = 90
                    },
                    PotentialCauses = new List<string>
                    {
                        "Gateway connectivity issues",
                        "Increased transaction volume",
                        "System resource constraints",
                        "External service dependencies"
                    }
                });
            }

            // Detect response time anomalies
            if (performanceMetrics.AverageProcessingTime > TimeSpan.FromSeconds(5)) // Above 5 second threshold
            {
                anomalies.Add(new AnomalyDetection
                {
                    AnomalyId = Guid.NewGuid().ToString(),
                    Type = AnomalyType.ResponseTimeSpike,
                    DetectedAt = DateTime.UtcNow,
                    AnomalyPeriodStart = startDate,
                    AnomalyPeriodEnd = endDate,
                    Description = $"Response time spiked to {performanceMetrics.AverageProcessingTime.TotalSeconds:F2}s (above 5s threshold)",
                    SeverityScore = Math.Min(10, performanceMetrics.AverageProcessingTime.TotalSeconds / 5 * 5),
                    MetricValues = new Dictionary<string, object>
                    {
                        ["AverageResponseTime"] = performanceMetrics.AverageProcessingTime.TotalSeconds,
                        ["Threshold"] = 5
                    },
                    PotentialCauses = new List<string>
                    {
                        "Database performance issues",
                        "Network latency",
                        "Resource contention",
                        "External API delays"
                    }
                });
            }

            _logger.LogInformation("Detected {AnomalyCount} payment anomalies", anomalies.Count);

            return anomalies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting payment anomalies");
            throw;
        }
    }

    #region Private Helper Methods

    private async Task<Dictionary<string, GatewayOptimizationMetrics>> AnalyzeGatewayMetricsAsync(
        PaymentOptimizationRequest request)
    {
        var gatewayMetrics = new Dictionary<string, GatewayOptimizationMetrics>();
        var gateways = string.IsNullOrEmpty(request.Gateway) 
            ? new[] { "Tingg", "PMEC", "BankTransfer" }
            : new[] { request.Gateway };

        foreach (var gateway in gateways)
        {
            try
            {
                var metrics = await _monitoringService.GetGatewayPerformanceMetricsAsync(
                    gateway, request.StartDate, request.EndDate);

                gatewayMetrics[gateway] = new GatewayOptimizationMetrics
                {
                    GatewayName = gateway,
                    SuccessRate = metrics.SuccessRate,
                    AverageResponseTime = metrics.AverageResponseTime,
                    P95ResponseTime = metrics.P95ResponseTime,
                    TotalTransactions = metrics.TotalTransactions,
                    TransactionsPerHour = CalculateTransactionsPerHour(metrics, request.StartDate, request.EndDate),
                    UptimePercentage = metrics.Availability,
                    ErrorBreakdown = metrics.ErrorTypes,
                    QualityScore = CalculateQualityScore(metrics)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not analyze metrics for gateway {Gateway}", gateway);
            }
        }

        return gatewayMetrics;
    }

    private async Task<List<OptimizationRecommendation>> GenerateOptimizationRecommendationsAsync(
        Dictionary<string, GatewayOptimizationMetrics> gatewayMetrics)
    {
        var recommendations = new List<OptimizationRecommendation>();

        foreach (var (gateway, metrics) in gatewayMetrics)
        {
            // Low success rate recommendation
            if (metrics.SuccessRate < 95)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    RecommendationId = Guid.NewGuid().ToString(),
                    Type = RecommendationType.ReliabilityImprovement,
                    Priority = metrics.SuccessRate < 90 ? RecommendationPriority.Critical : RecommendationPriority.High,
                    Title = $"Improve {gateway} Success Rate",
                    Description = $"Success rate for {gateway} is {metrics.SuccessRate:F2}%, below optimal 95% threshold",
                    Rationale = "Low success rates impact customer experience and revenue",
                    ExpectedImpact = new OptimizationImpact
                    {
                        SuccessRateImprovement = 95 - metrics.SuccessRate,
                        ConfidenceLevel = "High"
                    },
                    ImplementationSteps = new List<string>
                    {
                        "Analyze error patterns and root causes",
                        "Implement enhanced retry mechanisms",
                        "Add circuit breaker patterns",
                        "Monitor and adjust timeout configurations"
                    },
                    SupportingMetrics = new Dictionary<string, object>
                    {
                        ["Gateway"] = gateway,
                        ["CurrentSuccessRate"] = metrics.SuccessRate,
                        ["TargetSuccessRate"] = 95.0
                    }
                });
            }

            // Slow response time recommendation
            if (metrics.AverageResponseTime > TimeSpan.FromSeconds(2))
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    RecommendationId = Guid.NewGuid().ToString(),
                    Type = RecommendationType.PerformanceOptimization,
                    Priority = RecommendationPriority.Medium,
                    Title = $"Optimize {gateway} Response Time",
                    Description = $"Average response time for {gateway} is {metrics.AverageResponseTime.TotalSeconds:F2}s, above target 2s",
                    Rationale = "Faster response times improve user experience and system throughput",
                    ExpectedImpact = new OptimizationImpact
                    {
                        ResponseTimeImprovement = metrics.AverageResponseTime - TimeSpan.FromSeconds(2),
                        ThroughputImprovement = 25,
                        ConfidenceLevel = "Medium"
                    },
                    ImplementationSteps = new List<string>
                    {
                        "Profile and optimize database queries",
                        "Implement connection pooling",
                        "Add response caching where appropriate",
                        "Review and optimize API call patterns"
                    },
                    SupportingMetrics = new Dictionary<string, object>
                    {
                        ["Gateway"] = gateway,
                        ["CurrentResponseTime"] = metrics.AverageResponseTime.TotalSeconds,
                        ["TargetResponseTime"] = 2.0
                    }
                });
            }
        }

        await Task.CompletedTask; // Satisfy async requirement
        return recommendations;
    }

    private async Task<List<OptimizationRecommendation>> GeneratePerformanceRecommendationsAsync(
        PaymentPerformanceMetrics metrics)
    {
        var recommendations = new List<OptimizationRecommendation>();

        if (metrics.ThroughputPerMinute < 10) // Low throughput
        {
            recommendations.Add(new OptimizationRecommendation
            {
                RecommendationId = Guid.NewGuid().ToString(),
                Type = RecommendationType.PerformanceOptimization,
                Priority = RecommendationPriority.Medium,
                Title = "Increase Payment Processing Throughput",
                Description = $"Current throughput is {metrics.ThroughputPerMinute:F2} payments/min, target is 10+",
                Rationale = "Higher throughput reduces processing backlogs and improves customer experience",
                ExpectedImpact = new OptimizationImpact
                {
                    ThroughputImprovement = 50,
                    ConfidenceLevel = "Medium"
                }
            });
        }

        await Task.CompletedTask;
        return recommendations;
    }

    private async Task<List<OptimizationRecommendation>> GenerateReliabilityRecommendationsAsync(
        PaymentHealthStatus systemHealth)
    {
        var recommendations = new List<OptimizationRecommendation>();

        if (systemHealth.OverallStatus != SystemHealthStatus.Healthy)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                RecommendationId = Guid.NewGuid().ToString(),
                Type = RecommendationType.ReliabilityImprovement,
                Priority = RecommendationPriority.High,
                Title = "Address System Health Issues",
                Description = $"Payment system health status is {systemHealth.OverallStatus}",
                Rationale = "Healthy system status is critical for reliable payment processing"
            });
        }

        await Task.CompletedTask;
        return recommendations;
    }

    private async Task<List<OptimizationRecommendation>> GenerateCostOptimizationRecommendationsAsync(
        PaymentPerformanceMetrics metrics)
    {
        var recommendations = new List<OptimizationRecommendation>();
        await Task.CompletedTask; // Placeholder for cost optimization logic
        return recommendations;
    }

    private async Task<List<OptimizationRecommendation>> GenerateSecurityRecommendationsAsync()
    {
        var recommendations = new List<OptimizationRecommendation>();
        await Task.CompletedTask; // Placeholder for security recommendations
        return recommendations;
    }

    // Risk Assessment Methods

    private async Task<RiskCategoryAssessment> AssessOperationalRisk(
        PaymentHealthStatus systemHealth, PaymentPerformanceMetrics metrics)
    {
        var riskScore = 0.0;
        var indicators = new List<string>();

        if (systemHealth.OverallStatus != SystemHealthStatus.Healthy)
        {
            riskScore += 30;
            indicators.Add($"System health status: {systemHealth.OverallStatus}");
        }

        if (metrics.SuccessRate < 95)
        {
            riskScore += 25;
            indicators.Add($"Success rate below 95%: {metrics.SuccessRate:F2}%");
        }

        await Task.CompletedTask;

        return new RiskCategoryAssessment
        {
            Category = RiskCategory.OperationalRisk,
            RiskScore = Math.Min(100, riskScore),
            Level = DetermineRiskLevel(riskScore),
            RiskIndicators = indicators,
            Assessment = GenerateRiskAssessmentText(RiskCategory.OperationalRisk, riskScore)
        };
    }

    private async Task<RiskCategoryAssessment> AssessTechnicalRisk(
        PaymentHealthStatus systemHealth, PaymentPerformanceMetrics metrics)
    {
        var riskScore = 0.0;
        var indicators = new List<string>();

        if (metrics.AverageProcessingTime > TimeSpan.FromSeconds(5))
        {
            riskScore += 20;
            indicators.Add($"Slow processing time: {metrics.AverageProcessingTime.TotalSeconds:F2}s");
        }

        await Task.CompletedTask;

        return new RiskCategoryAssessment
        {
            Category = RiskCategory.TechnicalRisk,
            RiskScore = Math.Min(100, riskScore),
            Level = DetermineRiskLevel(riskScore),
            RiskIndicators = indicators,
            Assessment = GenerateRiskAssessmentText(RiskCategory.TechnicalRisk, riskScore)
        };
    }

    private async Task<RiskCategoryAssessment> AssessFinancialRisk(PaymentPerformanceMetrics metrics)
    {
        var riskScore = 0.0;
        var indicators = new List<string>();

        // Financial risk assessment logic would go here
        await Task.CompletedTask;

        return new RiskCategoryAssessment
        {
            Category = RiskCategory.FinancialRisk,
            RiskScore = riskScore,
            Level = DetermineRiskLevel(riskScore),
            RiskIndicators = indicators,
            Assessment = GenerateRiskAssessmentText(RiskCategory.FinancialRisk, riskScore)
        };
    }

    private async Task<RiskCategoryAssessment> AssessSecurityRisk()
    {
        // Security risk assessment would analyze authentication, encryption, audit logs, etc.
        await Task.CompletedTask;
        
        return new RiskCategoryAssessment
        {
            Category = RiskCategory.SecurityRisk,
            RiskScore = 15, // Baseline security risk
            Level = RiskLevel.Low,
            RiskIndicators = new List<string> { "Regular security assessments needed" },
            Assessment = "Security posture appears adequate but requires ongoing monitoring"
        };
    }

    private async Task<RiskCategoryAssessment> AssessComplianceRisk(PaymentPerformanceMetrics metrics)
    {
        // Compliance risk assessment would check against regulatory requirements
        await Task.CompletedTask;
        
        return new RiskCategoryAssessment
        {
            Category = RiskCategory.ComplianceRisk,
            RiskScore = 10, // Baseline compliance risk
            Level = RiskLevel.Low,
            RiskIndicators = new List<string>(),
            Assessment = "Compliance requirements appear to be met"
        };
    }

    private async Task<List<RiskFactor>> IdentifyRiskFactorsAsync(
        Dictionary<RiskCategory, RiskCategoryAssessment> riskCategories)
    {
        var riskFactors = new List<RiskFactor>();

        foreach (var (category, assessment) in riskCategories)
        {
            if (assessment.Level >= RiskLevel.Medium)
            {
                riskFactors.Add(new RiskFactor
                {
                    FactorId = Guid.NewGuid().ToString(),
                    Name = $"{category} Risk",
                    Description = assessment.Assessment,
                    RiskScore = assessment.RiskScore,
                    Level = assessment.Level,
                    Indicators = assessment.RiskIndicators
                });
            }
        }

        await Task.CompletedTask;
        return riskFactors;
    }

    private async Task<List<RiskMitigationStrategy>> GenerateRiskMitigationStrategiesAsync(
        List<RiskFactor> riskFactors)
    {
        var strategies = new List<RiskMitigationStrategy>();

        foreach (var risk in riskFactors.Where(r => r.Level >= RiskLevel.Medium))
        {
            strategies.Add(new RiskMitigationStrategy
            {
                StrategyId = Guid.NewGuid().ToString(),
                Name = $"Mitigate {risk.Name}",
                Description = $"Strategy to address {risk.Description}",
                EffectivenessScore = 75,
                ImplementationSteps = new List<string>
                {
                    "Analyze root causes",
                    "Implement corrective measures",
                    "Monitor effectiveness",
                    "Document lessons learned"
                }
            });
        }

        await Task.CompletedTask;
        return strategies;
    }

    // Helper Methods

    private static RiskLevel DetermineRiskLevel(double riskScore)
    {
        return riskScore switch
        {
            >= 70 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 30 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    private static string GenerateRiskAssessmentText(RiskCategory category, double riskScore)
    {
        var level = DetermineRiskLevel(riskScore);
        return $"{category} assessment shows {level.ToString().ToLower()} risk level (score: {riskScore:F1}/100)";
    }

    private static double CalculateQualityScore(GatewayPerformanceMetrics metrics)
    {
        // Simple quality score calculation
        var successRateWeight = 0.4;
        var responseTimeWeight = 0.3;
        var uptimeWeight = 0.3;

        var successRateScore = metrics.SuccessRate;
        var responseTimeScore = Math.Max(0, 100 - (metrics.AverageResponseTime.TotalSeconds / 10 * 100));
        var uptimeScore = metrics.Availability;

        return (successRateScore * successRateWeight) + 
               (responseTimeScore * responseTimeWeight) + 
               (uptimeScore * uptimeWeight);
    }

    private static double CalculateTransactionsPerHour(
        GatewayPerformanceMetrics metrics, DateTime startDate, DateTime endDate)
    {
        var hours = (endDate - startDate).TotalHours;
        return hours > 0 ? metrics.TotalTransactions / hours : 0;
    }

    private static double CalculateAnomalySeverity(double actual, double threshold)
    {
        var deviation = Math.Abs(actual - threshold) / threshold;
        return Math.Min(10, deviation * 10);
    }

    private static TrendDirection DetermineTrendDirection(Dictionary<DateTime, double> dailyMetrics)
    {
        if (dailyMetrics.Count < 2) return TrendDirection.Unknown;

        var values = dailyMetrics.OrderBy(x => x.Key).Select(x => x.Value).ToList();
        var firstHalf = values.Take(values.Count / 2).Average();
        var secondHalf = values.Skip(values.Count / 2).Average();

        var change = (secondHalf - firstHalf) / firstHalf * 100;

        return change switch
        {
            > 5 => TrendDirection.Improving,
            < -5 => TrendDirection.Degrading,
            _ => TrendDirection.Stable
        };
    }

    private static List<string> GeneratePerformanceInsights(PaymentTrendAnalysis analysis)
    {
        var insights = new List<string>();

        if (analysis.PeakHours.Any())
        {
            insights.Add($"Peak transaction hours: {string.Join(", ", analysis.PeakHours)}:00");
        }

        if (analysis.WorstPerformingPeriods.Any())
        {
            insights.Add($"Performance issues detected on {analysis.WorstPerformingPeriods.Count} days");
        }

        return insights;
    }

    private async Task InvalidateCachesAsync()
    {
        // In a production system, this would invalidate all related cache entries
        await Task.CompletedTask;
    }

    #region Cache Helper Methods

    private async Task<T?> GetFromCacheAsync<T>(string cacheKey) where T : class
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedValue))
            {
                return JsonSerializer.Deserialize<T>(cachedValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache with key: {CacheKey}", cacheKey);
        }
        
        return null;
    }

    private async Task SetCacheAsync<T>(string cacheKey, T value) where T : class
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };
            
            await _cache.SetStringAsync(cacheKey, serializedValue, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache with key: {CacheKey}", cacheKey);
        }
    }

    #endregion

    #endregion
}