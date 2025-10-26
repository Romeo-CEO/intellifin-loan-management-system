using IntelliFin.Collections.Domain.Entities;

namespace IntelliFin.Collections.Application.Services;

public interface IPaymentProcessingService
{
    /// <summary>
    /// Processes a payment transaction and applies it to installments.
    /// </summary>
    Task<Guid> ProcessPaymentAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves payment history for a loan.
    /// </summary>
    Task<List<PaymentTransaction>> GetPaymentHistoryAsync(
        Guid loanId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles a payment transaction.
    /// </summary>
    Task ReconcilePaymentAsync(
        Guid paymentTransactionId,
        string reconciledBy,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unreconciled payment transactions.
    /// </summary>
    Task<List<PaymentTransaction>> GetUnreconciledPaymentsAsync(
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
