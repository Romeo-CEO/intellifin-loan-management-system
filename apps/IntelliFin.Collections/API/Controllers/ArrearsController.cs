using IntelliFin.Collections.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.Collections.API.Controllers;

[ApiController]
[Route("api/collections/arrears")]
public class ArrearsController : ControllerBase
{
    private readonly IArrearsClassificationService _classificationService;
    private readonly ILogger<ArrearsController> _logger;

    public ArrearsController(
        IArrearsClassificationService classificationService,
        ILogger<ArrearsController> logger)
    {
        _classificationService = classificationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets classification history for a loan.
    /// </summary>
    [HttpGet("loan/{loanId}/history")]
    [ProducesResponseType(typeof(List<ClassificationHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassificationHistory(
        Guid loanId,
        CancellationToken cancellationToken)
    {
        var history = await _classificationService.GetClassificationHistoryAsync(loanId, cancellationToken);
        
        var dtos = history.Select(h => new ClassificationHistoryDto
        {
            Id = h.Id,
            LoanId = h.LoanId,
            PreviousClassification = h.PreviousClassification,
            NewClassification = h.NewClassification,
            DaysPastDue = h.DaysPastDue,
            OutstandingBalance = h.OutstandingBalance,
            ProvisionRate = h.ProvisionRate,
            ProvisionAmount = h.ProvisionAmount,
            IsNonAccrual = h.IsNonAccrual,
            ClassifiedAt = h.ClassifiedAt,
            Reason = h.Reason
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets summary of loans by arrears classification.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ArrearsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArrearsSummary(CancellationToken cancellationToken)
    {
        var summary = await _classificationService.GetArrearsSummaryAsync(cancellationToken);
        
        var dto = new ArrearsSummaryDto
        {
            TotalLoans = summary.Values.Sum(),
            ByClassification = summary
        };

        return Ok(dto);
    }

    /// <summary>
    /// Manually triggers classification for a specific loan (for testing/admin purposes).
    /// </summary>
    [HttpPost("loan/{loanId}/classify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClassifyLoan(
        Guid loanId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _classificationService.ClassifyLoanAsync(loanId, cancellationToken);
            return Ok(new { Message = "Loan classified successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }
}

public record ClassificationHistoryDto
{
    public Guid Id { get; init; }
    public Guid LoanId { get; init; }
    public string PreviousClassification { get; init; } = string.Empty;
    public string NewClassification { get; init; } = string.Empty;
    public int DaysPastDue { get; init; }
    public decimal OutstandingBalance { get; init; }
    public decimal ProvisionRate { get; init; }
    public decimal ProvisionAmount { get; init; }
    public bool IsNonAccrual { get; init; }
    public DateTime ClassifiedAt { get; init; }
    public string? Reason { get; init; }
}

public record ArrearsSummaryDto
{
    public int TotalLoans { get; init; }
    public Dictionary<string, int> ByClassification { get; init; } = new();
}
