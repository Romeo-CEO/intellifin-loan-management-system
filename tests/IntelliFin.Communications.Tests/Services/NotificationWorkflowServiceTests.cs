using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Text.Json;

namespace IntelliFin.Communications.Tests.Services;

public class NotificationWorkflowServiceTests
{
    private readonly Mock<ILogger<NotificationWorkflowService>> _loggerMock;
    private readonly Mock<ISmsService> _smsServiceMock;
    private readonly Mock<ISmsTemplateService> _templateServiceMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly NotificationWorkflowService _workflowService;

    public NotificationWorkflowServiceTests()
    {
        _loggerMock = new Mock<ILogger<NotificationWorkflowService>>();
        _smsServiceMock = new Mock<ISmsService>();
        _templateServiceMock = new Mock<ISmsTemplateService>();
        _cacheMock = new Mock<IDistributedCache>();

        _workflowService = new NotificationWorkflowService(
            _loggerMock.Object,
            _smsServiceMock.Object,
            _templateServiceMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task TriggerLoanApplicationStatusNotificationAsync_WhenClientNotOptedOut_ShouldSendNotification()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var applicationNumber = "APP001";
        var status = "Approved";
        var message = "Your application has been approved";
        var phoneNumber = "260761234567";

        var optOutStatus = new SmsOptOutStatus
        {
            IsOptedOut = false,
            OptedOutTypes = new List<SmsNotificationType>()
        };

        var expectedResponse = new SmsNotificationResponse
        {
            NotificationId = Guid.NewGuid().ToString(),
            Status = SmsDeliveryStatus.Sent
        };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        _cacheMock.Setup(x => x.GetStringAsync($"client:phone:{clientId}", default))
            .ReturnsAsync(phoneNumber);

        _smsServiceMock.Setup(x => x.SendTemplatedSmsAsync(
            "loan_status_en", 
            phoneNumber, 
            It.IsAny<Dictionary<string, object>>(),
            SmsNotificationType.LoanApplicationStatus, 
            default))
            .ReturnsAsync(expectedResponse);

        // Act
        await _workflowService.TriggerLoanApplicationStatusNotificationAsync(clientId, applicationNumber, status, message);

        // Assert
        _smsServiceMock.Verify(x => x.SendTemplatedSmsAsync(
            "loan_status_en",
            phoneNumber,
            It.Is<Dictionary<string, object>>(d => 
                d["applicationNumber"].ToString() == applicationNumber &&
                d["status"].ToString() == status &&
                d["message"].ToString() == message),
            SmsNotificationType.LoanApplicationStatus,
            default), Times.Once);
    }

    [Fact]
    public async Task TriggerLoanApplicationStatusNotificationAsync_WhenClientOptedOut_ShouldNotSendNotification()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var applicationNumber = "APP001";
        var status = "Approved";
        var message = "Your application has been approved";

        var optOutStatus = new SmsOptOutStatus
        {
            IsOptedOut = true,
            OptedOutTypes = [SmsNotificationType.LoanApplicationStatus]
        };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        // Act
        await _workflowService.TriggerLoanApplicationStatusNotificationAsync(clientId, applicationNumber, status, message);

        // Assert
        _smsServiceMock.Verify(x => x.SendTemplatedSmsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<SmsNotificationType>(),
            default), Times.Never);
    }

    [Fact]
    public async Task TriggerPaymentReminderNotificationAsync_WithValidData_ShouldSendNotificationWithCorrectTemplate()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var loanNumber = "LN001";
        var amount = 1500.75m;
        var dueDate = DateTime.Now.AddDays(3);
        var phoneNumber = "260761234567";

        var optOutStatus = new SmsOptOutStatus { IsOptedOut = false };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        _cacheMock.Setup(x => x.GetStringAsync($"client:phone:{clientId}", default))
            .ReturnsAsync(phoneNumber);

        var expectedResponse = new SmsNotificationResponse
        {
            NotificationId = Guid.NewGuid().ToString(),
            Status = SmsDeliveryStatus.Sent
        };

        _smsServiceMock.Setup(x => x.SendTemplatedSmsAsync(
            "payment_reminder_en",
            phoneNumber,
            It.IsAny<Dictionary<string, object>>(),
            SmsNotificationType.PaymentReminder,
            default))
            .ReturnsAsync(expectedResponse);

        // Act
        await _workflowService.TriggerPaymentReminderNotificationAsync(clientId, loanNumber, amount, dueDate);

        // Assert
        _smsServiceMock.Verify(x => x.SendTemplatedSmsAsync(
            "payment_reminder_en",
            phoneNumber,
            It.Is<Dictionary<string, object>>(d =>
                d["amount"].ToString() == amount.ToString("F2") &&
                d["dueDate"].ToString() == dueDate.ToString("dd/MM/yyyy") &&
                d["loanNumber"].ToString() == loanNumber),
            SmsNotificationType.PaymentReminder,
            default), Times.Once);
    }

    [Fact]
    public async Task TriggerPaymentConfirmationNotificationAsync_WithValidData_ShouldIncludeAllRequiredFields()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var loanNumber = "LN001";
        var amount = 1500.00m;
        var paymentDate = DateTime.Now;
        var remainingBalance = 8500.00m;
        var phoneNumber = "260761234567";

        var optOutStatus = new SmsOptOutStatus { IsOptedOut = false };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        _cacheMock.Setup(x => x.GetStringAsync($"client:phone:{clientId}", default))
            .ReturnsAsync(phoneNumber);

        var expectedResponse = new SmsNotificationResponse
        {
            NotificationId = Guid.NewGuid().ToString(),
            Status = SmsDeliveryStatus.Sent
        };

        _smsServiceMock.Setup(x => x.SendTemplatedSmsAsync(
            "payment_confirmation_en",
            phoneNumber,
            It.IsAny<Dictionary<string, object>>(),
            SmsNotificationType.PaymentConfirmation,
            default))
            .ReturnsAsync(expectedResponse);

        // Act
        await _workflowService.TriggerPaymentConfirmationNotificationAsync(clientId, loanNumber, amount, paymentDate, remainingBalance);

        // Assert
        _smsServiceMock.Verify(x => x.SendTemplatedSmsAsync(
            "payment_confirmation_en",
            phoneNumber,
            It.Is<Dictionary<string, object>>(d =>
                d["amount"].ToString() == amount.ToString("F2") &&
                d["loanNumber"].ToString() == loanNumber &&
                d["paymentDate"].ToString() == paymentDate.ToString("dd/MM/yyyy") &&
                d["remainingBalance"].ToString() == remainingBalance.ToString("F2")),
            SmsNotificationType.PaymentConfirmation,
            default), Times.Once);
    }

    [Fact]
    public async Task TriggerOverduePaymentNotificationAsync_WithValidData_ShouldIncludeDaysOverdue()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var loanNumber = "LN001";
        var amount = 1500.00m;
        var daysOverdue = 15;
        var phoneNumber = "260761234567";

        var optOutStatus = new SmsOptOutStatus { IsOptedOut = false };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        _cacheMock.Setup(x => x.GetStringAsync($"client:phone:{clientId}", default))
            .ReturnsAsync(phoneNumber);

        var expectedResponse = new SmsNotificationResponse
        {
            NotificationId = Guid.NewGuid().ToString(),
            Status = SmsDeliveryStatus.Sent
        };

        _smsServiceMock.Setup(x => x.SendTemplatedSmsAsync(
            "overdue_payment_en",
            phoneNumber,
            It.IsAny<Dictionary<string, object>>(),
            SmsNotificationType.OverduePayment,
            default))
            .ReturnsAsync(expectedResponse);

        // Act
        await _workflowService.TriggerOverduePaymentNotificationAsync(clientId, loanNumber, amount, daysOverdue);

        // Assert
        _smsServiceMock.Verify(x => x.SendTemplatedSmsAsync(
            "overdue_payment_en",
            phoneNumber,
            It.Is<Dictionary<string, object>>(d =>
                d["amount"].ToString() == amount.ToString("F2") &&
                d["loanNumber"].ToString() == loanNumber &&
                d["daysOverdue"].ToString() == daysOverdue.ToString()),
            SmsNotificationType.OverduePayment,
            default), Times.Once);
    }

    [Fact]
    public async Task IsClientOptedOutAsync_WhenClientOptedOutOfSpecificType_ShouldReturnTrue()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var optOutStatus = new SmsOptOutStatus
        {
            IsOptedOut = true,
            OptedOutTypes = [SmsNotificationType.PaymentReminder, SmsNotificationType.OverduePayment]
        };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        // Act
        var result = await _workflowService.IsClientOptedOutAsync(clientId, SmsNotificationType.PaymentReminder);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsClientOptedOutAsync_WhenClientNotOptedOutOfSpecificType_ShouldReturnFalse()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var optOutStatus = new SmsOptOutStatus
        {
            IsOptedOut = true,
            OptedOutTypes = [SmsNotificationType.PaymentReminder]
        };

        _cacheMock.Setup(x => x.GetStringAsync($"client:optout:{clientId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        // Act
        var result = await _workflowService.IsClientOptedOutAsync(clientId, SmsNotificationType.LoanApproval);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateClientNotificationPreferencesAsync_ShouldUpdateCacheWithCorrectData()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var phoneNumber = "260761234567";
        var optedOutTypes = new List<SmsNotificationType>
        {
            SmsNotificationType.PaymentReminder,
            SmsNotificationType.OverduePayment
        };

        _cacheMock.Setup(x => x.GetStringAsync($"client:phone:{clientId}", default))
            .ReturnsAsync(phoneNumber);

        // Act
        await _workflowService.UpdateClientNotificationPreferencesAsync(clientId, optedOutTypes);

        // Assert
        _cacheMock.Verify(x => x.SetStringAsync(
            $"client:optout:{clientId}",
            It.Is<string>(s => JsonSerializer.Deserialize<SmsOptOutStatus>(s)!.OptedOutTypes.Count == 2),
            It.IsAny<DistributedCacheEntryOptions>(),
            default), Times.Once);
    }

    [Fact]
    public async Task GetNotificationHistoryAsync_WhenHistoryCached_ShouldReturnCachedHistory()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        var expectedHistory = new List<SmsNotificationResponse>
        {
            new()
            {
                NotificationId = Guid.NewGuid().ToString(),
                Status = SmsDeliveryStatus.Delivered,
                SentAt = DateTime.UtcNow.AddDays(-2)
            }
        };

        var cacheKey = $"notifications:history:{clientId}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        _cacheMock.Setup(x => x.GetStringAsync(cacheKey, default))
            .ReturnsAsync(JsonSerializer.Serialize(expectedHistory));

        // Act
        var result = await _workflowService.GetNotificationHistoryAsync(clientId, startDate, endDate);

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedHistory[0].NotificationId, result[0].NotificationId);
        Assert.Equal(SmsDeliveryStatus.Delivered, result[0].Status);
    }

    [Fact]
    public async Task GetNotificationHistoryAsync_WhenHistoryNotCached_ShouldReturnEmptyList()
    {
        // Arrange
        var clientId = Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        var cacheKey = $"notifications:history:{clientId}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        _cacheMock.Setup(x => x.GetStringAsync(cacheKey, default))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _workflowService.GetNotificationHistoryAsync(clientId, startDate, endDate);

        // Assert
        Assert.Empty(result);
    }
}