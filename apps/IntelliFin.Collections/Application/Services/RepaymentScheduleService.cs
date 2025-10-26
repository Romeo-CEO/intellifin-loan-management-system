using IntelliFin.Collections.Domain.Entities;
using IntelliFin.Collections.Infrastructure.Persistence;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Application.Services;

public class RepaymentScheduleService : IRepaymentScheduleService
{
    private readonly CollectionsDbContext _dbContext;
    private readonly IAuditClient _auditClient;
    private readonly ILogger<RepaymentScheduleService> _logger;

    public RepaymentScheduleService(
        CollectionsDbContext dbContext,
        IAuditClient auditClient,
        ILogger<RepaymentScheduleService> logger)
    {
        _dbContext = dbContext;
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task<Guid> GenerateScheduleAsync(
        Guid loanId,
        Guid clientId,
        string productCode,
        decimal principalAmount,
        decimal interestRate,
        int termMonths,
        DateTime firstPaymentDate,
        string correlationId,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating repayment schedule for loan {LoanId}, client {ClientId}, amount {Amount}",
            loanId, clientId, principalAmount);

        // Check if schedule already exists
        var existingSchedule = await _dbContext.RepaymentSchedules
            .FirstOrDefaultAsync(s => s.LoanId == loanId, cancellationToken);

        if (existingSchedule != null)
        {
            _logger.LogWarning("Repayment schedule already exists for loan {LoanId}", loanId);
            return existingSchedule.Id;
        }

        // Calculate maturity date
        var maturityDate = firstPaymentDate.AddMonths(termMonths - 1);

        // Create repayment schedule
        var schedule = new RepaymentSchedule
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            ClientId = clientId,
            ProductCode = productCode,
            PrincipalAmount = principalAmount,
            InterestRate = interestRate,
            TermMonths = termMonths,
            RepaymentFrequency = "Monthly",
            FirstPaymentDate = firstPaymentDate,
            MaturityDate = maturityDate,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = generatedBy,
            CreatedAtUtc = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        // Generate installments
        var installments = GenerateInstallments(
            schedule.Id,
            principalAmount,
            interestRate,
            termMonths,
            firstPaymentDate);

        schedule.Installments = installments;

        // Save to database
        _dbContext.RepaymentSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit event
        await _auditClient.LogEventAsync(new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = generatedBy,
            Action = "RepaymentScheduleGenerated",
            EntityType = "RepaymentSchedule",
            EntityId = schedule.Id.ToString(),
            CorrelationId = correlationId,
            EventData = new
            {
                LoanId = loanId,
                ClientId = clientId,
                PrincipalAmount = principalAmount,
                TermMonths = termMonths,
                InstallmentsCount = installments.Count
            }
        }, cancellationToken);

        _logger.LogInformation(
            "Successfully generated repayment schedule {ScheduleId} for loan {LoanId} with {InstallmentCount} installments",
            schedule.Id, loanId, installments.Count);

        return schedule.Id;
    }

    public async Task<RepaymentSchedule?> GetScheduleByLoanIdAsync(
        Guid loanId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RepaymentSchedules
            .Include(s => s.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(s => s.LoanId == loanId, cancellationToken);
    }

    private List<Installment> GenerateInstallments(
        Guid scheduleId,
        decimal principalAmount,
        decimal annualInterestRate,
        int termMonths,
        DateTime firstPaymentDate)
    {
        var installments = new List<Installment>();
        var monthlyInterestRate = annualInterestRate / 12m;
        
        // Calculate monthly payment using amortization formula
        var monthlyPayment = CalculateMonthlyPayment(principalAmount, monthlyInterestRate, termMonths);
        
        var remainingBalance = principalAmount;
        var currentDate = firstPaymentDate;

        for (int i = 1; i <= termMonths; i++)
        {
            var interestDue = Math.Round(remainingBalance * monthlyInterestRate, 2);
            var principalDue = Math.Round(monthlyPayment - interestDue, 2);

            // Last installment adjustment for rounding
            if (i == termMonths)
            {
                principalDue = remainingBalance;
            }

            remainingBalance -= principalDue;

            var installment = new Installment
            {
                Id = Guid.NewGuid(),
                RepaymentScheduleId = scheduleId,
                InstallmentNumber = i,
                DueDate = currentDate,
                PrincipalDue = principalDue,
                InterestDue = interestDue,
                TotalDue = principalDue + interestDue,
                PrincipalPaid = 0,
                InterestPaid = 0,
                TotalPaid = 0,
                PrincipalBalance = remainingBalance,
                Status = "Pending",
                DaysPastDue = 0,
                CreatedAtUtc = DateTime.UtcNow
            };

            installments.Add(installment);
            currentDate = currentDate.AddMonths(1);
        }

        return installments;
    }

    private decimal CalculateMonthlyPayment(decimal principal, decimal monthlyRate, int months)
    {
        if (monthlyRate == 0)
        {
            return principal / months;
        }

        var numerator = principal * monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), months);
        var denominator = (decimal)Math.Pow((double)(1 + monthlyRate), months) - 1;
        
        return Math.Round(numerator / denominator, 2);
    }
}
