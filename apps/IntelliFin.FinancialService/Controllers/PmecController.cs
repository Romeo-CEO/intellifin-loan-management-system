using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/pmec")]
public class PmecController : ControllerBase
{
    private readonly IPmecService _pmecService;
    private readonly ILogger<PmecController> _logger;

    public PmecController(IPmecService pmecService, ILogger<PmecController> logger)
    {
        _pmecService = pmecService;
        _logger = logger;
    }

    /// <summary>
    /// Verify employee with PMEC
    /// </summary>
    [HttpPost("verify-employee")]
    public async Task<ActionResult<EmployeeVerificationResult>> VerifyEmployee([FromBody] EmployeeVerificationRequest request)
    {
        try
        {
            var result = await _pmecService.VerifyEmployeeAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying employee {EmployeeId}", request.EmployeeId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Submit deductions to PMEC
    /// </summary>
    [HttpPost("deductions/submit")]
    public async Task<ActionResult<DeductionSubmissionResult>> SubmitDeductions([FromBody] DeductionSubmissionRequest request)
    {
        try
        {
            var result = await _pmecService.SubmitDeductionsAsync(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting deductions for cycle {CycleId}", request.CycleId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Fetch deduction results from PMEC
    /// </summary>
    [HttpGet("deductions/{cycleId}/results")]
    public async Task<ActionResult<DeductionResultsResponse>> FetchDeductionResults(string cycleId)
    {
        try
        {
            var results = await _pmecService.FetchDeductionResultsAsync(cycleId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching deduction results for cycle {CycleId}", cycleId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Validate employee eligibility
    /// </summary>
    [HttpPost("validate-eligibility")]
    public async Task<ActionResult<bool>> ValidateEmployeeEligibility([FromBody] ValidateEligibilityRequest request)
    {
        try
        {
            var isEligible = await _pmecService.ValidateEmployeeEligibilityAsync(request.EmployeeId, request.NationalId);
            return Ok(isEligible);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating eligibility for employee {EmployeeId}", request.EmployeeId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get pending deductions
    /// </summary>
    [HttpGet("deductions/pending")]
    public async Task<ActionResult<IEnumerable<DeductionItem>>> GetPendingDeductions()
    {
        try
        {
            var deductions = await _pmecService.GetPendingDeductionsAsync();
            return Ok(deductions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending deductions");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get deduction status
    /// </summary>
    [HttpGet("deductions/{deductionId}/status")]
    public async Task<ActionResult<DeductionStatusResult>> GetDeductionStatus(string deductionId)
    {
        try
        {
            var status = await _pmecService.GetDeductionStatusAsync(deductionId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for deduction {DeductionId}", deductionId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Cancel deduction
    /// </summary>
    [HttpPost("deductions/{deductionId}/cancel")]
    public async Task<ActionResult<bool>> CancelDeduction(string deductionId, [FromBody] CancelDeductionRequest request)
    {
        try
        {
            var result = await _pmecService.CancelDeductionAsync(deductionId, request.Reason);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling deduction {DeductionId}", deductionId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Check PMEC connectivity
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<PmecHealthCheckResult>> CheckPmecConnectivity()
    {
        try
        {
            var healthCheck = await _pmecService.CheckPmecConnectivityAsync();
            return Ok(healthCheck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking PMEC connectivity");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class ValidateEligibilityRequest
{
    public string EmployeeId { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
}

public class CancelDeductionRequest
{
    public string Reason { get; set; } = string.Empty;
}
