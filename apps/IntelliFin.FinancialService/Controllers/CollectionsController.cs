using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/collections")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionsService _collectionsService;
    private readonly ILogger<CollectionsController> _logger;

    public CollectionsController(ICollectionsService collectionsService, ILogger<CollectionsController> logger)
    {
        _collectionsService = collectionsService;
        _logger = logger;
    }

    /// <summary>
    /// Get collections account details
    /// </summary>
    [HttpGet("accounts/{loanId}")]
    public async Task<ActionResult<CollectionsAccount>> GetCollectionsAccount(string loanId)
    {
        try
        {
            var account = await _collectionsService.GetCollectionsAccountAsync(loanId);
            if (account == null)
            {
                return NotFound($"Collections account for loan {loanId} not found");
            }
            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting collections account for loan {LoanId}", loanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Calculate Days Past Due (DPD)
    /// </summary>
    [HttpGet("accounts/{loanId}/dpd")]
    public async Task<ActionResult<DPDCalculationResult>> CalculateDPD(string loanId)
    {
        try
        {
            var result = await _collectionsService.CalculateDPDAsync(loanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating DPD for loan {LoanId}", loanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Classify loan according to BoZ guidelines
    /// </summary>
    [HttpGet("accounts/{loanId}/classification")]
    public async Task<ActionResult<BoZClassificationResult>> ClassifyLoan(string loanId)
    {
        try
        {
            var result = await _collectionsService.ClassifyLoanAsync(loanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying loan {LoanId}", loanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Calculate loan provisioning
    /// </summary>
    [HttpGet("accounts/{loanId}/provisioning")]
    public async Task<ActionResult<ProvisioningResult>> CalculateProvisioning(string loanId)
    {
        try
        {
            var result = await _collectionsService.CalculateProvisioningAsync(loanId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating provisioning for loan {LoanId}", loanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all overdue accounts
    /// </summary>
    [HttpGet("accounts/overdue")]
    public async Task<ActionResult<IEnumerable<CollectionsAccount>>> GetOverdueAccounts()
    {
        try
        {
            var accounts = await _collectionsService.GetOverdueAccountsAsync();
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue accounts");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Process deduction cycle
    /// </summary>
    [HttpPost("deduction-cycles")]
    public async Task<ActionResult<DeductionCycleResult>> ProcessDeductionCycle([FromBody] CreateDeductionCycleRequest request)
    {
        try
        {
            var result = await _collectionsService.ProcessDeductionCycleAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing deduction cycle for period {Period}", request.Period);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Record payment
    /// </summary>
    [HttpPost("payments")]
    public async Task<ActionResult<PaymentResult>> RecordPayment([FromBody] RecordPaymentRequest request)
    {
        try
        {
            var result = await _collectionsService.RecordPaymentAsync(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording payment for loan {LoanId}", request.LoanId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Generate collections report
    /// </summary>
    [HttpGet("reports")]
    public async Task<ActionResult<CollectionsReport>> GenerateCollectionsReport([FromQuery] DateTime reportDate)
    {
        try
        {
            var report = await _collectionsService.GenerateCollectionsReportAsync(reportDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating collections report for {ReportDate}", reportDate);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update account status
    /// </summary>
    [HttpPut("accounts/{loanId}/status")]
    public async Task<ActionResult<bool>> UpdateAccountStatus(string loanId, [FromBody] CollectionsStatus status)
    {
        try
        {
            var result = await _collectionsService.UpdateAccountStatusAsync(loanId, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for loan {LoanId}", loanId);
            return StatusCode(500, "Internal server error");
        }
    }
}
