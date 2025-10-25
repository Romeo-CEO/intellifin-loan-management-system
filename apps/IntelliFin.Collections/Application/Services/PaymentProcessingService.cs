using IntelliFin.Collections.Domain.Entities;
using IntelliFin.Collections.Infrastructure.Persistence;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Application.Services;

public class PaymentProcessingService : IPaymentProcessingService
{
    private readonly CollectionsDbContext _dbContext;
    private readonly IAuditClient _auditClient;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentProcessingService> _logger;

    public PaymentProcessingService(
        CollectionsDbContext dbContext,
        IAuditClient auditClient,
        INotificationService notificationService,
        ILogger<PaymentProcessingService> logger)
    {
        _dbContext = dbContext;
        _auditClient = auditClient;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Guid> ProcessPaymentAsync(
        Guid loanId,
        Guid clientId,
        string transactionReference,
        string paymentMethod,
        string paymentSource,
        decimal amount,
        DateTime transactionDate,
        string? externalReference,
        string? notes,
        string createdBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing payment for loan {LoanId}, amount {Amount}, reference {Reference}",
            loanId, amount, transactionReference);

        // Check for duplicate transaction reference
        var existingPayment = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(p => p.TransactionReference == transactionReference, cancellationToken);

        if (existingPayment != null)
        {
            _logger.LogWarning(
                "Payment transaction {Reference} already exists",
                transactionReference);
            return existingPayment.Id;
        }

        // Get repayment schedule with installments
        var schedule = await _dbContext.RepaymentSchedules
            .Include(s => s.Installments.OrderBy(i => i.InstallmentNumber))
            .FirstOrDefaultAsync(s => s.LoanId == loanId, cancellationToken);

        if (schedule == null)
        {
            throw new InvalidOperationException($"No repayment schedule found for loan {loanId}");
        }

        // Create payment transaction
        var paymentTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            ClientId = clientId,
            TransactionReference = transactionReference,
            PaymentMethod = paymentMethod,
            PaymentSource = paymentSource,
            Amount = amount,
            TransactionDate = transactionDate,
            ReceivedDate = DateTime.UtcNow,
            Status = "Pending",
            IsReconciled = false,
            ExternalReference = externalReference,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
            CorrelationId = correlationId
        };

        // Allocate payment to installments
        var remainingAmount = amount;
        var allocatedInstallments = new List<Guid>();

        foreach (var installment in schedule.Installments.Where(i => i.Status != "Paid").OrderBy(i => i.InstallmentNumber))
        {
            if (remainingAmount <= 0) break;

            var installmentOutstanding = installment.TotalDue - installment.TotalPaid;
            
            if (installmentOutstanding > 0)
            {
                var allocationAmount = Math.Min(remainingAmount, installmentOutstanding);
                
                // Allocate to interest first, then principal (standard practice)
                var interestOutstanding = installment.InterestDue - installment.InterestPaid;
                var interestPayment = Math.Min(allocationAmount, interestOutstanding);
                var principalPayment = allocationAmount - interestPayment;

                installment.InterestPaid += interestPayment;
                installment.PrincipalPaid += principalPayment;
                installment.TotalPaid += allocationAmount;
                installment.UpdatedAtUtc = DateTime.UtcNow;

                // Update installment status
                if (installment.TotalPaid >= installment.TotalDue)
                {
                    installment.Status = "Paid";
                    installment.PaidDate = DateTime.UtcNow;
                    installment.DaysPastDue = 0;
                }
                else if (installment.TotalPaid > 0)
                {
                    installment.Status = "PartiallyPaid";
                }

                paymentTransaction.InterestPortion += interestPayment;
                paymentTransaction.PrincipalPortion += principalPayment;
                
                remainingAmount -= allocationAmount;
                allocatedInstallments.Add(installment.Id);

                _logger.LogInformation(
                    "Allocated {Amount} to installment {InstallmentNumber} for loan {LoanId}",
                    allocationAmount, installment.InstallmentNumber, loanId);
            }
        }

        // If first installment gets full payment, link it
        if (allocatedInstallments.Count == 1)
        {
            var firstInstallment = schedule.Installments.First(i => allocatedInstallments.Contains(i.Id));
            if (firstInstallment.TotalPaid >= firstInstallment.TotalDue)
            {
                paymentTransaction.InstallmentId = firstInstallment.Id;
            }
        }

        // Create reconciliation task if there's a variance
        if (remainingAmount > 0.01m)
        {
            var reconciliationTask = new ReconciliationTask
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = paymentTransaction.Id,
                TaskType = "OverPayment",
                Status = "Pending",
                Description = $"Payment of {amount} exceeds outstanding balance by {remainingAmount}",
                ExpectedAmount = amount - remainingAmount,
                ActualAmount = amount,
                Variance = remainingAmount,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "System"
            };

            _dbContext.ReconciliationTasks.Add(reconciliationTask);
        }

        paymentTransaction.Status = "Confirmed";
        _dbContext.PaymentTransactions.Add(paymentTransaction);
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit event
        await _auditClient.LogEventAsync(new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = createdBy,
            Action = "PaymentProcessed",
            EntityType = "PaymentTransaction",
            EntityId = paymentTransaction.Id.ToString(),
            CorrelationId = correlationId,
            EventData = new
            {
                LoanId = loanId,
                Amount = amount,
                TransactionReference = transactionReference,
                PaymentMethod = paymentMethod,
                PrincipalPortion = paymentTransaction.PrincipalPortion,
                InterestPortion = paymentTransaction.InterestPortion,
                InstallmentsAffected = allocatedInstallments.Count
            }
        }, cancellationToken);

        _logger.LogInformation(
            "Successfully processed payment {PaymentId} for loan {LoanId}, allocated to {Count} installments",
            paymentTransaction.Id, loanId, allocatedInstallments.Count);

        // Send payment confirmation notification
        var remainingBalance = schedule.Installments.Sum(i => i.TotalDue - i.TotalPaid);
        await _notificationService.SendPaymentConfirmationAsync(
            loanId,
            clientId,
            amount,
            transactionDate,
            remainingBalance,
            correlationId,
            cancellationToken);

        return paymentTransaction.Id;
    }

    public async Task<List<PaymentTransaction>> GetPaymentHistoryAsync(
        Guid loanId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentTransactions
            .Where(p => p.LoanId == loanId)
            .OrderByDescending(p => p.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task ReconcilePaymentAsync(
        Guid paymentTransactionId,
        string reconciledBy,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(p => p.Id == paymentTransactionId, cancellationToken);

        if (payment == null)
        {
            throw new InvalidOperationException($"Payment transaction {paymentTransactionId} not found");
        }

        if (payment.IsReconciled)
        {
            _logger.LogWarning("Payment {PaymentId} is already reconciled", paymentTransactionId);
            return;
        }

        payment.IsReconciled = true;
        payment.ReconciledAt = DateTime.UtcNow;
        payment.ReconciledBy = reconciledBy;
        payment.Status = "Reconciled";
        payment.Notes = notes;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditClient.LogEventAsync(new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = reconciledBy,
            Action = "PaymentReconciled",
            EntityType = "PaymentTransaction",
            EntityId = paymentTransactionId.ToString(),
            CorrelationId = payment.CorrelationId,
            EventData = new
            {
                LoanId = payment.LoanId,
                Amount = payment.Amount,
                Notes = notes
            }
        }, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} reconciled by {ReconciledBy}",
            paymentTransactionId, reconciledBy);
    }

    public async Task<List<PaymentTransaction>> GetUnreconciledPaymentsAsync(
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentTransactions
            .Where(p => !p.IsReconciled)
            .OrderBy(p => p.TransactionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
