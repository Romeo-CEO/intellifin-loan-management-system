namespace IntelliFin.Collections.Application.Services;

public interface IRepaymentScheduleService
{
    /// <summary>
    /// Generates a repayment schedule for a disbursed loan.
    /// </summary>
    Task<Guid> GenerateScheduleAsync(
        Guid loanId,
        Guid clientId,
        string productCode,
        decimal principalAmount,
        decimal interestRate,
        int termMonths,
        DateTime firstPaymentDate,
        string correlationId,
        string generatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a repayment schedule by loan ID.
    /// </summary>
    Task<Domain.Entities.RepaymentSchedule?> GetScheduleByLoanIdAsync(
        Guid loanId,
        CancellationToken cancellationToken = default);
}
