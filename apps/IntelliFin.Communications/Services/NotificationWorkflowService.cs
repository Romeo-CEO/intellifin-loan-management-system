using IntelliFin.Communications.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.Communications.Services;

public class NotificationWorkflowService : INotificationWorkflowService
{
    private readonly ILogger<NotificationWorkflowService> _logger;
    private readonly ISmsService _smsService;
    private readonly ISmsTemplateService _templateService;
    private readonly IDistributedCache _cache;

    public NotificationWorkflowService(
        ILogger<NotificationWorkflowService> logger,
        ISmsService smsService,
        ISmsTemplateService templateService,
        IDistributedCache cache)
    {
        _logger = logger;
        _smsService = smsService;
        _templateService = templateService;
        _cache = cache;
    }

    public async Task TriggerLoanApplicationStatusNotificationAsync(string clientId, string applicationNumber,
        string status, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.LoanApplicationStatus, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of loan application status notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["applicationNumber"] = applicationNumber,
                ["status"] = status,
                ["message"] = message
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "loan_status_en", phoneNumber, templateData,
                SmsNotificationType.LoanApplicationStatus, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Loan application status notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending loan application status notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerPaymentReminderNotificationAsync(string clientId, string loanNumber,
        decimal amount, DateTime dueDate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.PaymentReminder, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of payment reminder notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["amount"] = amount.ToString("F2"),
                ["dueDate"] = dueDate.ToString("dd/MM/yyyy"),
                ["loanNumber"] = loanNumber
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "payment_reminder_en", phoneNumber, templateData,
                SmsNotificationType.PaymentReminder, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Payment reminder notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment reminder notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerPaymentConfirmationNotificationAsync(string clientId, string loanNumber,
        decimal amount, DateTime paymentDate, decimal remainingBalance, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.PaymentConfirmation, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of payment confirmation notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["amount"] = amount.ToString("F2"),
                ["loanNumber"] = loanNumber,
                ["paymentDate"] = paymentDate.ToString("dd/MM/yyyy"),
                ["remainingBalance"] = remainingBalance.ToString("F2")
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "payment_confirmation_en", phoneNumber, templateData,
                SmsNotificationType.PaymentConfirmation, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Payment confirmation notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment confirmation notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerOverduePaymentNotificationAsync(string clientId, string loanNumber,
        decimal amount, int daysOverdue, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.OverduePayment, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of overdue payment notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["amount"] = amount.ToString("F2"),
                ["loanNumber"] = loanNumber,
                ["daysOverdue"] = daysOverdue.ToString()
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "overdue_payment_en", phoneNumber, templateData,
                SmsNotificationType.OverduePayment, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Overdue payment notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending overdue payment notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerLoanApprovalNotificationAsync(string clientId, string applicationNumber,
        decimal amount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.LoanApproval, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of loan approval notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["applicationNumber"] = applicationNumber,
                ["amount"] = amount.ToString("F2")
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "loan_approval_en", phoneNumber, templateData,
                SmsNotificationType.LoanApproval, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Loan approval notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending loan approval notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerLoanDisbursementNotificationAsync(string clientId, string loanNumber,
        decimal amount, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.LoanDisbursement, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of loan disbursement notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["amount"] = amount.ToString("F2"),
                ["loanNumber"] = loanNumber
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "loan_disbursement_en", phoneNumber, templateData,
                SmsNotificationType.LoanDisbursement, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Loan disbursement notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending loan disbursement notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerPmecDeductionStatusNotificationAsync(string clientId, string loanNumber,
        string status, decimal amount, DateTime deductionDate, decimal balance, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.PmecDeductionStatus, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of PMEC deduction notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["status"] = status,
                ["amount"] = amount.ToString("F2"),
                ["deductionDate"] = deductionDate.ToString("dd/MM/yyyy"),
                ["loanNumber"] = loanNumber,
                ["balance"] = balance.ToString("F2")
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "pmec_deduction_en", phoneNumber, templateData,
                SmsNotificationType.PmecDeductionStatus, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("PMEC deduction status notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending PMEC deduction status notification to client {ClientId}", clientId);
        }
    }

    public async Task TriggerAccountBalanceNotificationAsync(string clientId, decimal balance,
        DateTime nextDueDate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsClientOptedOutAsync(clientId, SmsNotificationType.AccountBalance, cancellationToken))
            {
                _logger.LogInformation("Client {ClientId} opted out of account balance notifications", clientId);
                return;
            }

            var phoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarning("No phone number found for client {ClientId}", clientId);
                return;
            }

            var templateData = new Dictionary<string, object>
            {
                ["date"] = DateTime.Now.ToString("dd/MM/yyyy"),
                ["balance"] = balance.ToString("F2"),
                ["nextDueDate"] = nextDueDate.ToString("dd/MM/yyyy")
            };

            var response = await _smsService.SendTemplatedSmsAsync(
                "account_balance_en", phoneNumber, templateData,
                SmsNotificationType.AccountBalance, cancellationToken);

            await LogNotificationAsync(clientId, response, cancellationToken);

            _logger.LogInformation("Account balance notification sent to client {ClientId}. NotificationId: {NotificationId}",
                clientId, response.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending account balance notification to client {ClientId}", clientId);
        }
    }

    public async Task SchedulePaymentRemindersAsync(DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Scheduling payment reminders from {StartDate} to {EndDate}", startDate, endDate);

            // This would typically query the database for upcoming payment due dates
            // For now, we'll implement a placeholder
            
            var scheduledCount = 0;
            var cacheKey = $"payment:reminders:scheduled:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
            
            await _cache.SetStringAsync(cacheKey, scheduledCount.ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) },
                cancellationToken);

            _logger.LogInformation("Scheduled {Count} payment reminders", scheduledCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling payment reminders");
        }
    }

    public async Task ProcessScheduledNotificationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing scheduled notifications");

            // This would typically process scheduled notifications from a queue or database
            // For now, we'll implement a placeholder
            
            var processedCount = 0;
            _logger.LogInformation("Processed {Count} scheduled notifications", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled notifications");
        }
    }

    public async Task HandleFailedNotificationRetryAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Handling retry for failed notification {NotificationId}", notificationId);

            var deliveryStatus = await _smsService.GetDeliveryStatusAsync(notificationId, cancellationToken);
            if (deliveryStatus?.Status == SmsDeliveryStatus.Failed)
            {
                // This would typically implement retry logic based on the failure reason
                _logger.LogInformation("Notification {NotificationId} queued for retry", notificationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling failed notification retry for {NotificationId}", notificationId);
        }
    }

    public async Task<List<SmsNotificationResponse>> GetNotificationHistoryAsync(string clientId,
        DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"notifications:history:{clientId}:{startDate?.ToString("yyyyMMdd")}:{endDate?.ToString("yyyyMMdd")}";
            var cachedHistory = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedHistory))
            {
                return JsonSerializer.Deserialize<List<SmsNotificationResponse>>(cachedHistory) ?? new List<SmsNotificationResponse>();
            }

            // This would typically query the database for notification history
            var history = new List<SmsNotificationResponse>();

            // Cache for 1 hour
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(history),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
                cancellationToken);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification history for client {ClientId}", clientId);
            return new List<SmsNotificationResponse>();
        }
    }

    public async Task<bool> IsClientOptedOutAsync(string clientId, SmsNotificationType notificationType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"client:optout:{clientId}";
            var cachedOptOut = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedOptOut))
            {
                var optOutStatus = JsonSerializer.Deserialize<SmsOptOutStatus>(cachedOptOut);
                return optOutStatus?.IsOptedOut == true && 
                       optOutStatus.OptedOutTypes.Contains(notificationType);
            }

            return false; // Default: not opted out
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking opt-out status for client {ClientId}", clientId);
            return false; // Default: not opted out
        }
    }

    public async Task UpdateClientNotificationPreferencesAsync(string clientId, List<SmsNotificationType> optedOutTypes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var optOutStatus = new SmsOptOutStatus
            {
                PhoneNumber = await GetClientPhoneNumberAsync(clientId, cancellationToken) ?? "",
                IsOptedOut = optedOutTypes.Any(),
                OptedOutTypes = optedOutTypes,
                OptOutDate = optedOutTypes.Any() ? DateTime.UtcNow : null,
                OptOutReason = "Client preference update"
            };

            var cacheKey = $"client:optout:{clientId}";
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(optOutStatus),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
                cancellationToken);

            _logger.LogInformation("Updated notification preferences for client {ClientId}. Opted out types: {OptedOutTypes}",
                clientId, string.Join(", ", optedOutTypes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences for client {ClientId}", clientId);
        }
    }

    private async Task<string?> GetClientPhoneNumberAsync(string clientId, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"client:phone:{clientId}";
            var cachedPhone = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedPhone))
            {
                return cachedPhone;
            }

            // This would typically query the database for client phone number
            // For now, we'll return a placeholder
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving phone number for client {ClientId}", clientId);
            return null;
        }
    }

    private async Task LogNotificationAsync(string clientId, SmsNotificationResponse response, CancellationToken cancellationToken)
    {
        try
        {
            // This would typically log to database for audit and history tracking
            var logEntry = new
            {
                ClientId = clientId,
                NotificationId = response.NotificationId,
                Status = response.Status,
                Provider = response.UsedProvider,
                SentAt = response.SentAt,
                Cost = response.Cost
            };

            _logger.LogInformation("Notification logged for client {ClientId}: {@LogEntry}", clientId, logEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging notification for client {ClientId}", clientId);
        }
    }
}