using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Models;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.CreditAssessmentService.Controllers;

/// <summary>
/// API endpoints for initiating and managing credit assessments.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CreditAssessmentsController : ControllerBase
{
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly CreditAssessmentRequestValidator _requestValidator;
    private readonly ManualOverrideRequestValidator _overrideValidator;

    public CreditAssessmentsController(
        ICreditAssessmentService creditAssessmentService,
        CreditAssessmentRequestValidator requestValidator,
        ManualOverrideRequestValidator overrideValidator)
    {
        _creditAssessmentService = creditAssessmentService;
        _requestValidator = requestValidator;
        _overrideValidator = overrideValidator;
    }

    /// <summary>
    /// Performs a new credit assessment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreditAssessmentResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AssessAsync([FromBody] CreditAssessmentRequestDto request, CancellationToken cancellationToken)
    {
        var validationResult = await _requestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(validationResult.ToDictionary());
        }

        var assessment = await _creditAssessmentService.AssessAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { assessmentId = assessment.Id }, MapToResponse(assessment));
    }

    /// <summary>
    /// Retrieves a credit assessment by identifier.
    /// </summary>
    [HttpGet("{assessmentId:guid}")]
    [ProducesResponseType(typeof(CreditAssessmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await _creditAssessmentService.GetByIdAsync(assessmentId, cancellationToken);
        return assessment is null
            ? NotFound()
            : Ok(MapToResponse(assessment));
    }

    /// <summary>
    /// Retrieves assessments for a loan application ordered by recency.
    /// </summary>
    [HttpGet("loan-applications/{loanApplicationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CreditAssessmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForLoanApplicationAsync(Guid loanApplicationId, CancellationToken cancellationToken)
    {
        var assessments = await _creditAssessmentService.GetByLoanApplicationAsync(loanApplicationId, cancellationToken);
        return Ok(assessments.Select(MapToResponse));
    }

    /// <summary>
    /// Records a manual override decision for an assessment.
    /// </summary>
    [HttpPost("{assessmentId:guid}/overrides")]
    [ProducesResponseType(typeof(CreditAssessmentResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ManualOverrideAsync(Guid assessmentId, [FromBody] ManualOverrideRequestDto request, CancellationToken cancellationToken)
    {
        var validationResult = await _overrideValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(validationResult.ToDictionary());
        }

        var assessment = await _creditAssessmentService.RecordManualOverrideAsync(assessmentId, request, cancellationToken);
        return Ok(MapToResponse(assessment));
    }

    /// <summary>
    /// Invalidates an assessment due to KYC or compliance events.
    /// </summary>
    [HttpPost("{assessmentId:guid}/invalidate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InvalidateAsync(Guid assessmentId, [FromBody] string reason, CancellationToken cancellationToken)
    {
        await _creditAssessmentService.InvalidateAsync(assessmentId, reason, cancellationToken);
        return NoContent();
    }

    private static CreditAssessmentResponseDto MapToResponse(CreditAssessment assessment)
    {
        return new CreditAssessmentResponseDto
        {
            AssessmentId = assessment.Id,
            LoanApplicationId = assessment.LoanApplicationId,
            ClientId = assessment.ClientId,
            AssessedAt = assessment.AssessedAt,
            RiskGrade = assessment.RiskGrade,
            Decision = assessment.Decision,
            CreditScore = assessment.CreditScore,
            DebtToIncomeRatio = assessment.DebtToIncomeRatio,
            PaymentCapacity = assessment.PaymentCapacity,
            VaultConfigVersion = assessment.VaultConfigVersion,
            Factors = assessment.Factors.Select(f => new AssessmentFactorDto
            {
                Name = f.Name,
                Impact = f.Impact,
                Weight = f.Weight,
                Contribution = f.Contribution,
                Explanation = f.Explanation
            }).ToArray(),
            AuditTrail = assessment.AuditTrail.Select(a => new AuditEntryDto
            {
                OccurredAt = a.OccurredAt,
                Actor = a.Actor,
                Action = a.Action,
                Details = a.Details
            }).ToArray()
        };
    }
}
