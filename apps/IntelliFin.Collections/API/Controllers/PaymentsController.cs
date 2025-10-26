using IntelliFin.Collections.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.Collections.API.Controllers;

[ApiController]
[Route("api/collections/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentProcessingService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentProcessingService paymentService,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Manually records a payment transaction.
    /// </summary>
    [HttpPost("manual")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordManualPayment(
        [FromBody] ManualPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = HttpContext.TraceIdentifier;
            var createdBy = User.Identity?.Name ?? "Unknown";

            var paymentId = await _paymentService.ProcessPaymentAsync(
                loanId: request.LoanId,
                clientId: request.ClientId,
                transactionReference: request.TransactionReference,
                paymentMethod: request.PaymentMethod,
                paymentSource: request.PaymentSource,
                amount: request.Amount,
                transactionDate: request.TransactionDate,
                externalReference: request.ExternalReference,
                notes: request.Notes,
                createdBy: createdBy,
                correlationId: correlationId,
                cancellationToken: cancellationToken);

            return CreatedAtAction(
                nameof(GetPaymentHistory),
                new { loanId = request.LoanId },
                new PaymentResponseDto { PaymentId = paymentId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Gets payment history for a loan.
    /// </summary>
    [HttpGet("loan/{loanId}")]
    [ProducesResponseType(typeof(List<PaymentHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentHistory(
        Guid loanId,
        CancellationToken cancellationToken)
    {
        var payments = await _paymentService.GetPaymentHistoryAsync(loanId, cancellationToken);
        
        var dtos = payments.Select(p => new PaymentHistoryDto
        {
            Id = p.Id,
            TransactionReference = p.TransactionReference,
            PaymentMethod = p.PaymentMethod,
            PaymentSource = p.PaymentSource,
            Amount = p.Amount,
            PrincipalPortion = p.PrincipalPortion,
            InterestPortion = p.InterestPortion,
            TransactionDate = p.TransactionDate,
            Status = p.Status,
            IsReconciled = p.IsReconciled,
            ReconciledAt = p.ReconciledAt,
            ReconciledBy = p.ReconciledBy,
            Notes = p.Notes
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Reconciles a payment transaction.
    /// </summary>
    [HttpPost("{paymentId}/reconcile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReconcilePayment(
        Guid paymentId,
        [FromBody] ReconcilePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var reconciledBy = User.Identity?.Name ?? "Unknown";
            
            await _paymentService.ReconcilePaymentAsync(
                paymentId,
                reconciledBy,
                request.Notes,
                cancellationToken);

            return Ok(new { Message = "Payment reconciled successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Gets unreconciled payments for reconciliation workbench.
    /// </summary>
    [HttpGet("unreconciled")]
    [ProducesResponseType(typeof(List<PaymentHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreconciledPayments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentService.GetUnreconciledPaymentsAsync(
            pageNumber, pageSize, cancellationToken);
        
        var dtos = payments.Select(p => new PaymentHistoryDto
        {
            Id = p.Id,
            TransactionReference = p.TransactionReference,
            PaymentMethod = p.PaymentMethod,
            Amount = p.Amount,
            TransactionDate = p.TransactionDate,
            Status = p.Status,
            IsReconciled = p.IsReconciled,
            Notes = p.Notes
        }).ToList();

        return Ok(dtos);
    }
}

public record ManualPaymentRequest
{
    public Guid LoanId { get; init; }
    public Guid ClientId { get; init; }
    public string TransactionReference { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentSource { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime TransactionDate { get; init; }
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }
}

public record PaymentResponseDto
{
    public Guid PaymentId { get; init; }
}

public record PaymentHistoryDto
{
    public Guid Id { get; init; }
    public string TransactionReference { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string PaymentSource { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal PrincipalPortion { get; init; }
    public decimal InterestPortion { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsReconciled { get; init; }
    public DateTime? ReconciledAt { get; init; }
    public string? ReconciledBy { get; init; }
    public string? Notes { get; init; }
}

public record ReconcilePaymentRequest
{
    public string? Notes { get; init; }
}
