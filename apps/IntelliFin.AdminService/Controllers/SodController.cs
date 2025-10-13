using System.Security.Claims;
using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/sod")]
[Authorize]
public class SodController : ControllerBase
{
    private readonly ISodExceptionService _sodExceptionService;
    private readonly ILogger<SodController> _logger;

    public SodController(ISodExceptionService sodExceptionService, ILogger<SodController> logger)
    {
        _sodExceptionService = sodExceptionService;
        _logger = logger;
    }

    [HttpPost("exception-request")]
    [ProducesResponseType(typeof(SodExceptionResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestException([FromBody] SodExceptionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var requestorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var requestorName = User.FindFirstValue(ClaimTypes.Name) ?? requestorId;

        var response = await _sodExceptionService.RequestExceptionAsync(request, requestorId, requestorName, cancellationToken);

        return AcceptedAtAction(
            nameof(GetExceptionStatus),
            new { exceptionId = response.ExceptionId },
            response);
    }

    [HttpGet("exceptions/{exceptionId}")]
    [ProducesResponseType(typeof(SodExceptionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExceptionStatus(Guid exceptionId, CancellationToken cancellationToken)
    {
        var status = await _sodExceptionService.GetExceptionStatusAsync(exceptionId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPost("exceptions/{exceptionId}/approve")]
    [Authorize(Roles = "Compliance Officer")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveException(Guid exceptionId, [FromBody] SodExceptionReviewDto review, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var reviewerName = User.FindFirstValue(ClaimTypes.Name) ?? reviewerId;

        await _sodExceptionService.ApproveExceptionAsync(exceptionId, reviewerId, reviewerName, review.Comments, cancellationToken);

        _logger.LogInformation("SoD exception {ExceptionId} approved by {Reviewer}", exceptionId, reviewerId);
        return Ok(new { message = "SoD exception approved" });
    }

    [HttpPost("exceptions/{exceptionId}/reject")]
    [Authorize(Roles = "Compliance Officer")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectException(Guid exceptionId, [FromBody] SodExceptionReviewDto review, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var reviewerName = User.FindFirstValue(ClaimTypes.Name) ?? reviewerId;

        await _sodExceptionService.RejectExceptionAsync(exceptionId, reviewerId, reviewerName, review.Comments, cancellationToken);

        _logger.LogInformation("SoD exception {ExceptionId} rejected by {Reviewer}", exceptionId, reviewerId);
        return Ok(new { message = "SoD exception rejected" });
    }

    [HttpGet("compliance-report")]
    [Authorize(Roles = "Compliance Officer,Auditor")]
    [ProducesResponseType(typeof(SodComplianceReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplianceReport([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken cancellationToken)
    {
        var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
        var end = endDate ?? DateTime.UtcNow;

        var report = await _sodExceptionService.GenerateComplianceReportAsync(start, end, cancellationToken);
        return Ok(report);
    }

    [HttpGet("policies")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SodPolicyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicies(CancellationToken cancellationToken)
    {
        var policies = await _sodExceptionService.GetAllPoliciesAsync(cancellationToken);
        return Ok(policies);
    }

    [HttpPut("policies/{policyId}")]
    [Authorize(Roles = "Compliance Officer")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePolicy(int policyId, [FromBody] SodPolicyUpdateDto update, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _sodExceptionService.UpdatePolicyAsync(policyId, update, adminId, cancellationToken);

        return Ok(new { message = "SoD policy updated" });
    }
}

