using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using IntelliFin.Communications.Providers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using System.Text.Json;

namespace IntelliFin.Communications.Tests.Services;

public class SmsServiceTests
{
    private readonly Mock<ILogger<SmsService>> _loggerMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IAirtelSmsProvider> _airtelProviderMock;
    private readonly Mock<IMtnSmsProvider> _mtnProviderMock;
    private readonly Mock<IOptions<SmsRateLimitConfig>> _rateLimitOptionsMock;
    private readonly Mock<IOptions<Dictionary<SmsProvider, SmsProviderSettings>>> _providerSettingsOptionsMock;
    private readonly SmsService _smsService;

    public SmsServiceTests()
    {
        _loggerMock = new Mock<ILogger<SmsService>>();
        _cacheMock = new Mock<IDistributedCache>();
        _airtelProviderMock = new Mock<IAirtelSmsProvider>();
        _mtnProviderMock = new Mock<IMtnSmsProvider>();
        _rateLimitOptionsMock = new Mock<IOptions<SmsRateLimitConfig>>();
        _providerSettingsOptionsMock = new Mock<IOptions<Dictionary<SmsProvider, SmsProviderSettings>>>();

        // Setup default configurations
        _rateLimitOptionsMock.Setup(x => x.Value).Returns(new SmsRateLimitConfig
        {
            MaxSmsPerMinute = 60,
            MaxSmsPerHour = 1000,
            MaxSmsPerDay = 10000,
            DailyCostLimit = 1000m
        });

        _providerSettingsOptionsMock.Setup(x => x.Value).Returns(new Dictionary<SmsProvider, SmsProviderSettings>
        {
            [SmsProvider.Airtel] = new SmsProviderSettings
            {
                ApiUrl = "https://api.airtel.test",
                CostPerSms = 0.05m,
                IsActive = true
            },
            [SmsProvider.MTN] = new SmsProviderSettings
            {
                ApiUrl = "https://api.mtn.test",
                CostPerSms = 0.04m,
                IsActive = true
            }
        });

        _smsService = new SmsService(
            _loggerMock.Object,
            _cacheMock.Object,
            _airtelProviderMock.Object,
            _mtnProviderMock.Object,
            _rateLimitOptionsMock.Object,
            _providerSettingsOptionsMock.Object);
    }

    [Theory]
    [InlineData("260761234567", true)]
    [InlineData("260771234567", true)]
    [InlineData("0761234567", true)]
    [InlineData("0771234567", true)]
    [InlineData("260951234567", false)] // MTN number
    [InlineData("123456789", false)]
    [InlineData("", false)]
    public async Task ValidatePhoneNumberAsync_ShouldReturnCorrectValidation(string phoneNumber, bool expected)
    {
        // Act
        var result = await _smsService.ValidatePhoneNumberAsync(phoneNumber);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("260761234567", SmsProvider.Airtel)]
    [InlineData("260771234567", SmsProvider.Airtel)]
    [InlineData("260951234567", SmsProvider.MTN)]
    [InlineData("260961234567", SmsProvider.MTN)]
    [InlineData("260971234567", SmsProvider.MTN)]
    [InlineData("0761234567", SmsProvider.Airtel)]
    [InlineData("0951234567", SmsProvider.MTN)]
    public async Task GetOptimalProviderAsync_ShouldReturnCorrectProvider(string phoneNumber, SmsProvider expectedProvider)
    {
        // Act
        var result = await _smsService.GetOptimalProviderAsync(phoneNumber);

        // Assert
        Assert.Equal(expectedProvider, result);
    }

    [Fact]
    public async Task SendSmsAsync_WhenRateLimitExceeded_ShouldReturnFailedResponse()
    {
        // Arrange
        var request = new SmsNotificationRequest
        {
            PhoneNumber = "260761234567",
            Message = "Test message",
            NotificationType = SmsNotificationType.LoanApproval
        };

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync("1000"); // Rate limit exceeded

        // Act
        var result = await _smsService.SendSmsAsync(request);

        // Assert
        Assert.Equal(SmsDeliveryStatus.Failed, result.Status);
        Assert.Contains("Rate limit exceeded", result.ErrorMessage);
    }

    [Fact]
    public async Task SendSmsAsync_WhenUserOptedOut_ShouldReturnOptedOutResponse()
    {
        // Arrange
        var request = new SmsNotificationRequest
        {
            PhoneNumber = "260761234567",
            Message = "Test message",
            NotificationType = SmsNotificationType.PaymentReminder
        };

        var optOutStatus = new SmsOptOutStatus
        {
            IsOptedOut = true,
            OptedOutTypes = [SmsNotificationType.PaymentReminder]
        };

        _cacheMock.Setup(x => x.GetStringAsync($"sms:optout:{request.PhoneNumber}", default))
            .ReturnsAsync(JsonSerializer.Serialize(optOutStatus));

        // Act
        var result = await _smsService.SendSmsAsync(request);

        // Assert
        Assert.Equal(SmsDeliveryStatus.OptedOut, result.Status);
        Assert.Contains("opted out", result.ErrorMessage);
    }

    [Fact]
    public async Task SendSmsAsync_WithValidRequest_ShouldCallProviderAndReturnSuccess()
    {
        // Arrange
        var request = new SmsNotificationRequest
        {
            PhoneNumber = "260761234567",
            Message = "Test message",
            NotificationType = SmsNotificationType.LoanApproval
        };

        var expectedResponse = new SmsNotificationResponse
        {
            NotificationId = Guid.NewGuid().ToString(),
            Status = SmsDeliveryStatus.Sent,
            UsedProvider = SmsProvider.Airtel,
            SentAt = DateTime.UtcNow,
            Cost = 0.05m
        };

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string?)null);

        _airtelProviderMock.Setup(x => x.SendSmsAsync(It.IsAny<SmsNotificationRequest>(), default))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _smsService.SendSmsAsync(request);

        // Assert
        Assert.Equal(SmsDeliveryStatus.Sent, result.Status);
        Assert.Equal(SmsProvider.Airtel, result.UsedProvider);
        _airtelProviderMock.Verify(x => x.SendSmsAsync(It.IsAny<SmsNotificationRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task SendBulkSmsAsync_WithMultipleRequests_ShouldProcessAllAndReturnSummary()
    {
        // Arrange
        var bulkRequest = new BulkSmsRequest
        {
            BatchId = Guid.NewGuid().ToString(),
            BatchSize = 2,
            Notifications = new List<SmsNotificationRequest>
            {
                new() { PhoneNumber = "260761234567", Message = "Message 1" },
                new() { PhoneNumber = "260762345678", Message = "Message 2" },
                new() { PhoneNumber = "260763456789", Message = "Message 3" }
            }
        };

        var successResponse = new SmsNotificationResponse
        {
            NotificationId = Guid.NewGuid().ToString(),
            Status = SmsDeliveryStatus.Sent,
            Cost = 0.05m
        };

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string?)null);

        _airtelProviderMock.Setup(x => x.SendSmsAsync(It.IsAny<SmsNotificationRequest>(), default))
            .ReturnsAsync(successResponse);

        // Act
        var result = await _smsService.SendBulkSmsAsync(bulkRequest);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.True(result.CompletedAt.HasValue);
        Assert.Equal(0.15m, result.EstimatedCost); // 3 * 0.05
    }

    [Theory]
    [InlineData(SmsProvider.Airtel, 0.05)]
    [InlineData(SmsProvider.MTN, 0.04)]
    public async Task EstimateCostAsync_ShouldReturnCorrectCostForProvider(SmsProvider provider, decimal expectedCost)
    {
        // Arrange
        var phoneNumber = provider == SmsProvider.Airtel ? "260761234567" : "260951234567";
        var request = new SmsNotificationRequest
        {
            PhoneNumber = phoneNumber,
            Message = "Test message"
        };

        // Act
        var result = await _smsService.EstimateCostAsync(request);

        // Assert
        Assert.Equal(expectedCost, result);
    }

    [Fact]
    public async Task IsRateLimitExceededAsync_WhenUnderLimit_ShouldReturnFalse()
    {
        // Arrange
        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync("10"); // Under all limits

        // Act
        var result = await _smsService.IsRateLimitExceededAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsRateLimitExceededAsync_WhenOverMinuteLimit_ShouldReturnTrue()
    {
        // Arrange
        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync("70"); // Over minute limit of 60

        // Act
        var result = await _smsService.IsRateLimitExceededAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_WhenStatusCached_ShouldReturnCachedStatus()
    {
        // Arrange
        var notificationId = Guid.NewGuid().ToString();
        var expectedStatus = new SmsDeliveryReport
        {
            NotificationId = notificationId,
            Status = SmsDeliveryStatus.Delivered,
            Provider = SmsProvider.Airtel
        };

        _cacheMock.Setup(x => x.GetStringAsync($"sms:delivery:{notificationId}", default))
            .ReturnsAsync(JsonSerializer.Serialize(expectedStatus));

        // Act
        var result = await _smsService.GetDeliveryStatusAsync(notificationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(notificationId, result.NotificationId);
        Assert.Equal(SmsDeliveryStatus.Delivered, result.Status);
        Assert.Equal(SmsProvider.Airtel, result.Provider);
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_WhenStatusNotCached_ShouldReturnNull()
    {
        // Arrange
        var notificationId = Guid.NewGuid().ToString();

        _cacheMock.Setup(x => x.GetStringAsync($"sms:delivery:{notificationId}", default))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _smsService.GetDeliveryStatusAsync(notificationId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSmsAnalyticsAsync_ShouldReturnAnalyticsWithCorrectPeriod()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _smsService.GetSmsAnalyticsAsync(startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.StartDate);
        Assert.Equal(endDate, result.EndDate);
        Assert.Equal("Weekly", result.Period);
    }
}