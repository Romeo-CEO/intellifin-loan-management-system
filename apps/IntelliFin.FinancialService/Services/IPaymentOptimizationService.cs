using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// Payment optimization service interface
/// </summary>
public interface IPaymentOptimizationService
{
    Task<PaymentOptimizationResponse> AnalyzePaymentPerformanceAsync(PaymentOptimizationRequest request);
    Task<List<OptimizationRecommendation>> GetOptimizationRecommendationsAsync(string? gateway = null);
    Task<PaymentRiskAssessment> AssessPaymentRisksAsync(string? gateway = null);
    Task<bool> ImplementOptimizationAsync(string recommendationId, string implementedBy);
    Task<PaymentPerformanceTrends> GetPerformanceTrendsAsync(int daysBack = 30);
    Task<List<AnomalyDetection>> DetectAnomaliesAsync(DateTime startDate, DateTime endDate);
}