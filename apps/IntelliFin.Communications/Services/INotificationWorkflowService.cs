using IntelliFin.Communications.Models;

namespace IntelliFin.Communications.Services;

public interface INotificationWorkflowService
{
    Task TriggerLoanApplicationStatusNotificationAsync(string clientId, string applicationNumber, 
        string status, string message, CancellationToken cancellationToken = default);
    
    Task TriggerPaymentReminderNotificationAsync(string clientId, string loanNumber, 
        decimal amount, DateTime dueDate, CancellationToken cancellationToken = default);
    
    Task TriggerPaymentConfirmationNotificationAsync(string clientId, string loanNumber, 
        decimal amount, DateTime paymentDate, decimal remainingBalance, CancellationToken cancellationToken = default);
    
    Task TriggerOverduePaymentNotificationAsync(string clientId, string loanNumber, 
        decimal amount, int daysOverdue, CancellationToken cancellationToken = default);
    
    Task TriggerLoanApprovalNotificationAsync(string clientId, string applicationNumber, 
        decimal amount, CancellationToken cancellationToken = default);
    
    Task TriggerLoanDisbursementNotificationAsync(string clientId, string loanNumber, 
        decimal amount, CancellationToken cancellationToken = default);
    
    Task TriggerPmecDeductionStatusNotificationAsync(string clientId, string loanNumber, 
        string status, decimal amount, DateTime deductionDate, decimal balance, CancellationToken cancellationToken = default);
    
    Task TriggerAccountBalanceNotificationAsync(string clientId, decimal balance, 
        DateTime nextDueDate, CancellationToken cancellationToken = default);
    
    Task SchedulePaymentRemindersAsync(DateTime startDate, DateTime endDate, 
        CancellationToken cancellationToken = default);
    
    Task ProcessScheduledNotificationsAsync(CancellationToken cancellationToken = default);
    
    Task HandleFailedNotificationRetryAsync(string notificationId, CancellationToken cancellationToken = default);
    
    Task<List<SmsNotificationResponse>> GetNotificationHistoryAsync(string clientId, 
        DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    
    Task<bool> IsClientOptedOutAsync(string clientId, SmsNotificationType notificationType, 
        CancellationToken cancellationToken = default);
    
    Task UpdateClientNotificationPreferencesAsync(string clientId, List<SmsNotificationType> optedOutTypes, 
        CancellationToken cancellationToken = default);
}