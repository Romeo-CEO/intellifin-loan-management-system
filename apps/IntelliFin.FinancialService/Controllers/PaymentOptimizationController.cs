using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/payment-optimization")]
[Produces("application/json")]
public class PaymentOptimizationController : ControllerBase
{
    private readonly IPaymentRetryService _retryService;
    private readonly IPaymentReconciliationService _reconciliationService;
    private readonly IPaymentMonitoringService _monitoringService;
    private readonly ILogger<PaymentOptimizationController> _logger;

    public PaymentOptimizationController(
        IPaymentRetryService retryService,
        IPaymentReconciliationService reconciliationService,
        IPaymentMonitoringService monitoringService,
        ILogger<PaymentOptimizationController> logger)
    {
        _retryService = retryService;
        _reconciliationService = reconciliationService;
        _monitoringService = monitoringService;
        _logger = logger;
    }

    /// <summary>
    /// Process payment with automatic retry mechanism
    /// </summary>
    [HttpPost("payments/process-with-retry")]
    public async Task<ActionResult<PaymentProcessingResult>> ProcessPaymentWithRetryAsync(
        [FromBody] ProcessPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Processing payment with retry for loan {LoanId}", request.LoanId);
            var result = await _retryService.ProcessPaymentWithRetryAsync(request);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment with retry");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Process Tingg payment with automatic retry mechanism
    /// </summary>
    [HttpPost("tingg/process-with-retry")]
    public async Task<ActionResult<TinggPaymentResult>> ProcessTinggPaymentWithRetryAsync(
        [FromBody] TinggPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Processing Tingg payment with retry for transaction {TransactionId}", 
                request.MerchantTransactionId);
            var result = await _retryService.ProcessTinggPaymentWithRetryAsync(request);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Tingg payment with retry");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Submit PMEC deductions with automatic retry mechanism
    /// </summary>
    [HttpPost("pmec/submit-with-retry")]
    public async Task<ActionResult<DeductionSubmissionResult>> SubmitPmecDeductionsWithRetryAsync(
        [FromBody] DeductionSubmissionRequest request)
    {
        try
        {
            _logger.LogInformation("Submitting PMEC deductions with retry for cycle {CycleId}", request.CycleId);
            var result = await _retryService.SubmitPmecDeductionsWithRetryAsync(request);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting PMEC deductions with retry");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Reconcile all payments for a specific date
    /// </summary>
    [HttpPost("reconciliation/payments")]
    public async Task<ActionResult<ReconciliationSummary>> ReconcilePaymentsAsync(
        [FromQuery] DateTime reconciliationDate)
    {
        try
        {
            _logger.LogInformation("Starting payment reconciliation for {Date}", reconciliationDate);
            var result = await _reconciliationService.ReconcilePaymentsAsync(reconciliationDate);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling payments");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Reconcile Tingg payments for a specific date
    /// </summary>
    [HttpPost("reconciliation/tingg")]
    public async Task<ActionResult<ReconciliationSummary>> ReconcileTinggPaymentsAsync(
        [FromQuery] DateTime reconciliationDate)
    {
        try
        {
            _logger.LogInformation("Starting Tingg payment reconciliation for {Date}", reconciliationDate);
            var result = await _reconciliationService.ReconcileTinggPaymentsAsync(reconciliationDate);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling Tingg payments");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Reconcile PMEC deductions for a specific date
    /// </summary>
    [HttpPost("reconciliation/pmec")]
    public async Task<ActionResult<ReconciliationSummary>> ReconcilePmecDeductionsAsync(
        [FromQuery] DateTime reconciliationDate)
    {
        try
        {
            _logger.LogInformation("Starting PMEC deduction reconciliation for {Date}", reconciliationDate);
            var result = await _reconciliationService.ReconcilePmecDeductionsAsync(reconciliationDate);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling PMEC deductions");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Reconcile a specific payment
    /// </summary>
    [HttpPost("reconciliation/payment/{paymentId}")]
    public async Task<ActionResult<ReconciliationResult>> ReconcileSpecificPaymentAsync(
        string paymentId, [FromQuery] string externalReference)
    {
        try
        {
            _logger.LogInformation("Reconciling specific payment {PaymentId}", paymentId);
            var result = await _reconciliationService.ReconcileSpecificPaymentAsync(paymentId, externalReference);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling specific payment");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Get unreconciled payments
    /// </summary>
    [HttpGet("reconciliation/unreconciled")]
    public async Task<ActionResult<IEnumerable<UnreconciledPayment>>> GetUnreconciledPaymentsAsync(
        [FromQuery] int daysBack = 7)
    {
        try
        {
            _logger.LogInformation("Getting unreconciled payments for last {DaysBack} days", daysBack);
            var result = await _reconciliationService.GetUnreconciledPaymentsAsync(daysBack);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unreconciled payments");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Setup automatic reconciliation schedules
    /// </summary>
    [HttpPost("reconciliation/schedule")]
    public async Task<ActionResult> ScheduleAutomaticReconciliationAsync()
    {
        try
        {
            _logger.LogInformation("Setting up automatic reconciliation schedules");
            await _reconciliationService.ScheduleAutomaticReconciliationAsync();
            
            return Ok(new { message = "Automatic reconciliation schedules configured successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up automatic reconciliation");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Get payment performance metrics
    /// </summary>
    [HttpGet("monitoring/performance")]
    public async Task<ActionResult<PaymentPerformanceMetrics>> GetPaymentPerformanceMetricsAsync(
        [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Getting payment performance metrics from {StartDate} to {EndDate}", 
                startDate, endDate);
            var result = await _monitoringService.GetPaymentPerformanceMetricsAsync(startDate, endDate);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment performance metrics");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Get gateway-specific performance metrics
    /// </summary>
    [HttpGet("monitoring/gateway/{gatewayName}/performance")]
    public async Task<ActionResult<GatewayPerformanceMetrics>> GetGatewayPerformanceMetricsAsync(
        string gatewayName, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Getting performance metrics for gateway {GatewayName}", gatewayName);
            var result = await _monitoringService.GetGatewayPerformanceMetricsAsync(gatewayName, startDate, endDate);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gateway performance metrics");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Get payment system health status
    /// </summary>
    [HttpGet("monitoring/health")]
    public async Task<ActionResult<PaymentHealthStatus>> GetPaymentSystemHealthAsync()
    {
        try
        {
            _logger.LogInformation("Getting payment system health status");
            var result = await _monitoringService.GetPaymentSystemHealthAsync();
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment system health");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Get active payment alerts
    /// </summary>
    [HttpGet("monitoring/alerts")]
    public async Task<ActionResult<IEnumerable<PaymentAlert>>> GetActivePaymentAlertsAsync()
    {
        try
        {
            _logger.LogInformation("Getting active payment alerts");
            var result = await _monitoringService.GetActivePaymentAlertsAsync();
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active payment alerts");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Get payment trend analysis
    /// </summary>
    [HttpGet("monitoring/trends")]
    public async Task<ActionResult<PaymentTrendAnalysis>> GetPaymentTrendAnalysisAsync(
        [FromQuery] int daysBack = 30)
    {
        try
        {
            _logger.LogInformation("Getting payment trend analysis for last {DaysBack} days", daysBack);
            var result = await _monitoringService.GetPaymentTrendAnalysisAsync(daysBack);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment trend analysis");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Start performance monitoring
    /// </summary>
    [HttpPost("monitoring/start")]
    public async Task<ActionResult> StartPerformanceMonitoringAsync()
    {
        try
        {
            _logger.LogInformation("Starting payment performance monitoring");
            await _monitoringService.StartPerformanceMonitoringAsync();
            
            return Ok(new { message = "Payment performance monitoring started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting performance monitoring");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }
}