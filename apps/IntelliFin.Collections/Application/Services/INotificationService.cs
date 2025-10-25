namespace IntelliFin.Collections.Application.Services;

public interface INotificationService
{
    /// <summary>
    /// Sends payment reminder notification to customer.
    /// </summary>
    Task SendPaymentReminderAsync(
        Guid loanId,
        Guid clientId,
        decimal amountDue,
        DateTime dueDate,
        int daysPastDue,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends payment received confirmation.
    /// </summary>
    Task SendPaymentConfirmationAsync(
        Guid loanId,
        Guid clientId,
        decimal amountPaid,
        DateTime paymentDate,
        decimal remainingBalance,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends loan classification change notification.
    /// </summary>
    Task SendClassificationNotificationAsync(
        Guid loanId,
        Guid clientId,
        string newClassification,
        int daysPastDue,
        string correlationId,
        CancellationToken cancellationToken = default);
}
