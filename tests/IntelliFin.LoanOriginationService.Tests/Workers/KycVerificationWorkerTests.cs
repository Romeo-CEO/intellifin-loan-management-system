using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using IntelliFin.LoanOriginationService.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.LoanOriginationService.Tests.Workers;

public class KycVerificationWorkerTests
{
    private readonly Mock<IZeebeClient> _mockZeebeClient;
    private readonly Mock<IClientManagementClient> _mockClientManagementClient;
    private readonly Mock<ILogger<KycVerificationWorker>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;

    public KycVerificationWorkerTests()
    {
        _mockZeebeClient = new Mock<IZeebeClient>();
        _mockClientManagementClient = new Mock<IClientManagementClient>();
        _mockLogger = new Mock<ILogger<KycVerificationWorker>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        // Setup service provider to return scoped services
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IClientManagementClient)))
            .Returns(_mockClientManagementClient.Object);
        _mockServiceProvider.Setup(sp => sp.CreateScope()).Returns(_mockServiceScope.Object);
    }

    [Fact]
    public async Task HandleKycVerification_WithApprovedAndNotExpiredKyc_CompletesJobSuccessfully()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var approvedDate = DateTime.UtcNow.AddMonths(-6); // 6 months ago, not expired
        var jobKey = 12345L;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockCompleteCommand = new Mock<ICompleteJobCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { clientId = clientId.ToString() });
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);

        var verification = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Approved",
            KycApprovedAt = approvedDate,
            VerificationLevel = "Enhanced",
            RiskRating = "Low"
        };

        _mockClientManagementClient
            .Setup(c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verification);

        mockJobClient
            .Setup(jc => jc.NewCompleteCommand(jobKey))
            .Returns(mockCompleteCommand.Object);

        mockCompleteCommand
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockCompleteCommand.Object);

        mockCompleteCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act - Use reflection to call private method
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewCompleteCommand(jobKey), Times.Once);
        mockCompleteCommand.Verify(c => c.Variables(It.Is<string>(v => 
            v.Contains("\"kycVerified\":true") && 
            v.Contains("Enhanced") && 
            v.Contains("Low"))), Times.Once);
        mockCompleteCommand.Verify(c => c.Send(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_WithPendingKyc_ThrowsKycNotVerifiedError()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var jobKey = 12345L;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockThrowErrorCommand = new Mock<IThrowErrorCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { clientId = clientId.ToString() });
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);

        var verification = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Pending",
            KycApprovedAt = null
        };

        _mockClientManagementClient
            .Setup(c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verification);

        mockJobClient
            .Setup(jc => jc.NewThrowErrorCommand(jobKey))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorCode("KYC_NOT_VERIFIED"))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorMessage(It.IsAny<string>()))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewThrowErrorCommand(jobKey), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorCode("KYC_NOT_VERIFIED"), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorMessage(It.Is<string>(m => m.Contains("Pending"))), Times.Once);
        mockThrowErrorCommand.Verify(c => c.Send(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_WithRevokedKyc_ThrowsKycNotVerifiedError()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var jobKey = 12345L;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockThrowErrorCommand = new Mock<IThrowErrorCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { clientId = clientId.ToString() });
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);

        var verification = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Revoked",
            KycApprovedAt = DateTime.UtcNow.AddMonths(-3)
        };

        _mockClientManagementClient
            .Setup(c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verification);

        mockJobClient
            .Setup(jc => jc.NewThrowErrorCommand(jobKey))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorCode("KYC_NOT_VERIFIED"))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorMessage(It.IsAny<string>()))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewThrowErrorCommand(jobKey), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorCode("KYC_NOT_VERIFIED"), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorMessage(It.Is<string>(m => m.Contains("Revoked"))), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_WithExpiredKyc_ThrowsKycExpiredError()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var jobKey = 12345L;
        var approvedDate = DateTime.UtcNow.AddMonths(-13); // 13 months ago, expired

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockThrowErrorCommand = new Mock<IThrowErrorCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { clientId = clientId.ToString() });
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);

        var verification = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Approved",
            KycApprovedAt = approvedDate
        };

        _mockClientManagementClient
            .Setup(c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verification);

        mockJobClient
            .Setup(jc => jc.NewThrowErrorCommand(jobKey))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorCode("KYC_EXPIRED"))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorMessage(It.IsAny<string>()))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewThrowErrorCommand(jobKey), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorCode("KYC_EXPIRED"), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorMessage(It.Is<string>(m => 
            m.Contains("expired") && m.Contains("12 months"))), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_WithClientManagementServiceException_FailsJobWithRetry()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var jobKey = 12345L;
        var currentRetries = 3;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockFailCommand = new Mock<IFailJobCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { clientId = clientId.ToString() });
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);
        mockJob.Setup(j => j.Retries).Returns(currentRetries);

        _mockClientManagementClient
            .Setup(c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClientManagementServiceException("Service unavailable"));

        mockJobClient
            .Setup(jc => jc.NewFailCommand(jobKey))
            .Returns(mockFailCommand.Object);

        mockFailCommand
            .Setup(c => c.Retries(currentRetries - 1))
            .Returns(mockFailCommand.Object);

        mockFailCommand
            .Setup(c => c.ErrorMessage(It.IsAny<string>()))
            .Returns(mockFailCommand.Object);

        mockFailCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewFailCommand(jobKey), Times.Once);
        mockFailCommand.Verify(c => c.Retries(currentRetries - 1), Times.Once);
        mockFailCommand.Verify(c => c.ErrorMessage(It.Is<string>(m => m.Contains("unavailable"))), Times.Once);
        mockFailCommand.Verify(c => c.Send(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_WithMissingClientId_ThrowsMissingClientIdError()
    {
        // Arrange
        var jobKey = 12345L;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockThrowErrorCommand = new Mock<IThrowErrorCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { someOtherField = "value" }); // Missing clientId
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);

        mockJobClient
            .Setup(jc => jc.NewThrowErrorCommand(jobKey))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorCode("MISSING_CLIENT_ID"))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorMessage(It.IsAny<string>()))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewThrowErrorCommand(jobKey), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorCode("MISSING_CLIENT_ID"), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_WithInvalidJsonVariables_ThrowsInvalidJobVariablesError()
    {
        // Arrange
        var jobKey = 12345L;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockThrowErrorCommand = new Mock<IThrowErrorCommandStep1>();

        mockJob.Setup(j => j.Variables).Returns("invalid json {{{"); // Invalid JSON
        mockJob.Setup(j => j.Key).Returns(jobKey);

        mockJobClient
            .Setup(jc => jc.NewThrowErrorCommand(jobKey))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorCode("INVALID_JOB_VARIABLES"))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.ErrorMessage(It.IsAny<string>()))
            .Returns(mockThrowErrorCommand.Object);

        mockThrowErrorCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        // Act
        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert
        mockJobClient.Verify(jc => jc.NewThrowErrorCommand(jobKey), Times.Once);
        mockThrowErrorCommand.Verify(c => c.ErrorCode("INVALID_JOB_VARIABLES"), Times.Once);
    }

    [Fact]
    public async Task HandleKycVerification_CalledMultipleTimes_IsIdempotent()
    {
        // Arrange - This test verifies idempotency by calling handler multiple times
        var clientId = Guid.NewGuid();
        var approvedDate = DateTime.UtcNow.AddMonths(-6);
        var jobKey = 12345L;

        var mockJob = new Mock<IJob>();
        var mockJobClient = new Mock<IJobClient>();
        var mockCompleteCommand = new Mock<ICompleteJobCommandStep1>();

        var jobVariables = JsonSerializer.Serialize(new { clientId = clientId.ToString() });
        mockJob.Setup(j => j.Variables).Returns(jobVariables);
        mockJob.Setup(j => j.Key).Returns(jobKey);

        var verification = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Approved",
            KycApprovedAt = approvedDate,
            VerificationLevel = "Basic",
            RiskRating = "Medium"
        };

        _mockClientManagementClient
            .Setup(c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verification);

        mockJobClient
            .Setup(jc => jc.NewCompleteCommand(jobKey))
            .Returns(mockCompleteCommand.Object);

        mockCompleteCommand
            .Setup(c => c.Variables(It.IsAny<string>()))
            .Returns(mockCompleteCommand.Object);

        mockCompleteCommand
            .Setup(c => c.Send(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new KycVerificationWorker(
            _mockZeebeClient.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);

        var method = typeof(KycVerificationWorker).GetMethod(
            "HandleKycVerificationAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act - Call multiple times to test idempotency
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;
        await (Task)method!.Invoke(worker, new object[] { mockJobClient.Object, mockJob.Object })!;

        // Assert - Both calls should result in same outcome (complete command called twice)
        // Zeebe handles deduplication on its side
        mockJobClient.Verify(jc => jc.NewCompleteCommand(jobKey), Times.Exactly(2));
        _mockClientManagementClient.Verify(
            c => c.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()), 
            Times.Exactly(2)); // Read-only operation is idempotent
    }
}
