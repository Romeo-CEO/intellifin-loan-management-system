using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentProcessingService _paymentProcessingService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentProcessingService paymentProcessingService, ILogger<PaymentsController> logger)
    {
        _paymentProcessingService = paymentProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Process payment
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult<PaymentProcessingResult>> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        try
        {
            var result = await _paymentProcessingService.ProcessPaymentAsync(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for loan {LoanId}", request.LoanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Reconcile payment
    /// </summary>
    [HttpPost("{paymentId}/reconcile")]
    public async Task<ActionResult<PaymentReconciliationResult>> ReconcilePayment(string paymentId)
    {
        try
        {
            var result = await _paymentProcessingService.ReconcilePaymentAsync(paymentId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling payment {PaymentId}", paymentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get payment history for a loan
    /// </summary>
    [HttpGet("loans/{loanId}/history")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetPaymentHistory(string loanId)
    {
        try
        {
            var payments = await _paymentProcessingService.GetPaymentHistoryAsync(loanId);
            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment history for loan {LoanId}", loanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get payment status
    /// </summary>
    [HttpGet("{paymentId}/status")]
    public async Task<ActionResult<PaymentStatusResult>> GetPaymentStatus(string paymentId)
    {
        try
        {
            var status = await _paymentProcessingService.GetPaymentStatusAsync(paymentId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for payment {PaymentId}", paymentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Process refund
    /// </summary>
    [HttpPost("refunds")]
    public async Task<ActionResult<RefundResult>> ProcessRefund([FromBody] ProcessRefundRequest request)
    {
        try
        {
            var result = await _paymentProcessingService.ProcessRefundAsync(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for payment {PaymentId}", request.PaymentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Validate payment method
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<bool>> ValidatePaymentMethod([FromBody] ValidatePaymentMethodRequest request)
    {
        try
        {
            var isValid = await _paymentProcessingService.ValidatePaymentMethodAsync(request.PaymentMethod, request.Amount);
            return Ok(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating payment method {Method}", request.PaymentMethod);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Process Tingg payment
    /// </summary>
    [HttpPost("tingg")]
    public async Task<ActionResult<TinggPaymentResult>> ProcessTinggPayment([FromBody] TinggPaymentRequest request)
    {
        try
        {
            var result = await _paymentProcessingService.ProcessTinggPaymentAsync(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Tingg payment");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Check payment gateway health
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<PaymentGatewayHealthResult>> CheckPaymentGatewayHealth()
    {
        try
        {
            var healthCheck = await _paymentProcessingService.CheckPaymentGatewayHealthAsync();
            return Ok(healthCheck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking payment gateway health");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class ValidatePaymentMethodRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
}
