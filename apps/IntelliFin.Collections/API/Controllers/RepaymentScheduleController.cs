using IntelliFin.Collections.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.Collections.API.Controllers;

[ApiController]
[Route("api/collections/schedules")]
public class RepaymentScheduleController : ControllerBase
{
    private readonly IRepaymentScheduleService _scheduleService;
    private readonly ILogger<RepaymentScheduleController> _logger;

    public RepaymentScheduleController(
        IRepaymentScheduleService scheduleService,
        ILogger<RepaymentScheduleController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the repayment schedule for a loan.
    /// </summary>
    [HttpGet("loan/{loanId}")]
    [ProducesResponseType(typeof(RepaymentScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByLoanId(Guid loanId, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleService.GetScheduleByLoanIdAsync(loanId, cancellationToken);
        
        if (schedule == null)
        {
            return NotFound(new { Message = $"No repayment schedule found for loan {loanId}" });
        }

        var dto = new RepaymentScheduleDto
        {
            Id = schedule.Id,
            LoanId = schedule.LoanId,
            ClientId = schedule.ClientId,
            ProductCode = schedule.ProductCode,
            PrincipalAmount = schedule.PrincipalAmount,
            InterestRate = schedule.InterestRate,
            TermMonths = schedule.TermMonths,
            FirstPaymentDate = schedule.FirstPaymentDate,
            MaturityDate = schedule.MaturityDate,
            GeneratedAt = schedule.GeneratedAt,
            Installments = schedule.Installments.Select(i => new InstallmentDto
            {
                Id = i.Id,
                InstallmentNumber = i.InstallmentNumber,
                DueDate = i.DueDate,
                PrincipalDue = i.PrincipalDue,
                InterestDue = i.InterestDue,
                TotalDue = i.TotalDue,
                PrincipalPaid = i.PrincipalPaid,
                InterestPaid = i.InterestPaid,
                TotalPaid = i.TotalPaid,
                PrincipalBalance = i.PrincipalBalance,
                Status = i.Status,
                DaysPastDue = i.DaysPastDue,
                PaidDate = i.PaidDate
            }).ToList()
        };

        return Ok(dto);
    }
}

public record RepaymentScheduleDto
{
    public Guid Id { get; init; }
    public Guid LoanId { get; init; }
    public Guid ClientId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public decimal PrincipalAmount { get; init; }
    public decimal InterestRate { get; init; }
    public int TermMonths { get; init; }
    public DateTime FirstPaymentDate { get; init; }
    public DateTime MaturityDate { get; init; }
    public DateTime GeneratedAt { get; init; }
    public List<InstallmentDto> Installments { get; init; } = new();
}

public record InstallmentDto
{
    public Guid Id { get; init; }
    public int InstallmentNumber { get; init; }
    public DateTime DueDate { get; init; }
    public decimal PrincipalDue { get; init; }
    public decimal InterestDue { get; init; }
    public decimal TotalDue { get; init; }
    public decimal PrincipalPaid { get; init; }
    public decimal InterestPaid { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal PrincipalBalance { get; init; }
    public string Status { get; init; } = string.Empty;
    public int DaysPastDue { get; init; }
    public DateTime? PaidDate { get; init; }
}
