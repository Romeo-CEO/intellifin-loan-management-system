using IntelliFin.TreasuryService.Services;
using IntelliFin.TreasuryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace IntelliFin.TreasuryService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TreasuryController : ControllerBase
{
    private readonly ITreasuryService _treasuryService;
    private readonly ILogger<TreasuryController> _logger;

    public TreasuryController(
        ITreasuryService treasuryService,
        ILogger<TreasuryController> logger)
    {
        _treasuryService = treasuryService;
        _logger = logger;
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "IntelliFin.TreasuryService",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("version")]
    [AllowAnonymous]
    public IActionResult Version()
    {
        return Ok(new
        {
            service = "IntelliFin.TreasuryService",
            version = "1.0.0",
            status = "Operational",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("transactions/{id}")]
    public async Task<IActionResult> GetTransaction(int id)
    {
        try
        {
            var transaction = await _treasuryService.GetTransactionByIdAsync(id);
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Transaction not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transaction: {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("transactions/by-transaction-id/{transactionId}")]
    public async Task<IActionResult> GetTransactionByTransactionId(Guid transactionId)
    {
        try
        {
            var transaction = await _treasuryService.GetTransactionByTransactionIdAsync(transactionId);
            return Ok(transaction);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Transaction not found: {TransactionId}", transactionId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transaction: {TransactionId}", transactionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var transaction = await _treasuryService.CreateTransactionAsync(
                request.TransactionType,
                request.Amount,
                request.Currency,
                request.CorrelationId ?? Guid.NewGuid().ToString());

            return CreatedAtAction(
                nameof(GetTransaction),
                new { id = transaction.Id },
                transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("disbursements")]
    public async Task<IActionResult> ProcessDisbursement([FromBody] DisbursementRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _treasuryService.ProcessDisbursementAsync(
                request.DisbursementId,
                request.Amount,
                request.BankAccountNumber,
                request.BankCode,
                request.CorrelationId);

            return Accepted(new
            {
                message = "Disbursement processing initiated",
                disbursementId = request.DisbursementId,
                correlationId = request.CorrelationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing disbursement: {DisbursementId}", request.DisbursementId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPut("transactions/{transactionId}/status")]
    public async Task<IActionResult> UpdateTransactionStatus(Guid transactionId, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _treasuryService.UpdateTransactionStatusAsync(
                transactionId,
                request.Status,
                request.ErrorMessage);

            return Ok(new { message = "Transaction status updated successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Transaction not found for status update: {TransactionId}", transactionId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating transaction status: {TransactionId}", transactionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

// Request/Response models
public class CreateTransactionRequest
{
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MWK";
    public string? CorrelationId { get; set; } = Guid.NewGuid().ToString();
}

public class DisbursementRequest
{
    public Guid DisbursementId { get; set; }
    public decimal Amount { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string? CorrelationId { get; set; } = Guid.NewGuid().ToString();
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
