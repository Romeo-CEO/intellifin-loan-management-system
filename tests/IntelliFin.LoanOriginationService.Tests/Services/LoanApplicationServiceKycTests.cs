using System;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.LoanOriginationService.Tests.Services;

/// <summary>
/// Unit tests for KYC validation logic in LoanApplicationService.
/// Tests all KYC statuses (Pending, Expired, Revoked, Not Found) and expiration scenarios.
/// </summary>
public class LoanApplicationServiceKycTests
{
    private readonly Mock<ILogger<LoanApplicationService>> _loggerMock;
    private readonly Mock<ILoanProductService> _productServiceMock;
    private readonly Mock<ICreditAssessmentService> _creditAssessmentServiceMock;
    private readonly Mock<IWorkflowService> _workflowServiceMock;
    private readonly Mock<IComplianceService> _complianceServiceMock;
    private readonly Mock<ILoanApplicationRepository> _applicationRepositoryMock;
    private readonly Mock<ILoanVersioningService> _versioningServiceMock;
    private readonly Mock<IClientManagementClient> _clientManagementClientMock;

    public LoanApplicationServiceKycTests()
    {
        _loggerMock = new Mock<ILogger<LoanApplicationService>>();
        _productServiceMock = new Mock<ILoanProductService>();
        _creditAssessmentServiceMock = new Mock<ICreditAssessmentService>();
        _workflowServiceMock = new Mock<IWorkflowService>();
        _complianceServiceMock = new Mock<IComplianceService>();
        _applicationRepositoryMock = new Mock<ILoanApplicationRepository>();
        _versioningServiceMock = new Mock<ILoanVersioningService>();
        _clientManagementClientMock = new Mock<IClientManagementClient>();
    }

    [Fact]
    public async Task CreateApplicationAsync_WithKycStatusPending_ThrowsKycNotVerifiedException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientVerificationResponse
            {
                ClientId = clientId,
                KycStatus = "Pending",
                AmlStatus = "Pending"
            });

        var service = CreateService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KycNotVerifiedException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));

        Assert.Equal(clientId, exception.ClientId);
        Assert.Equal("Pending", exception.KycStatus);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithKycStatusRevoked_ThrowsKycNotVerifiedException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientVerificationResponse
            {
                ClientId = clientId,
                KycStatus = "Revoked",
                AmlStatus = "Flagged"
            });

        var service = CreateService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KycNotVerifiedException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));

        Assert.Equal(clientId, exception.ClientId);
        Assert.Equal("Revoked", exception.KycStatus);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithKycStatusExpired_ThrowsKycNotVerifiedException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientVerificationResponse
            {
                ClientId = clientId,
                KycStatus = "Expired",
                AmlStatus = "Cleared"
            });

        var service = CreateService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KycNotVerifiedException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));

        Assert.Equal(clientId, exception.ClientId);
        Assert.Equal("Expired", exception.KycStatus);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithKycApprovedMoreThan12MonthsAgo_ThrowsKycExpiredException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);
        var kycApprovedAt = DateTime.UtcNow.AddMonths(-13); // 13 months ago

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientVerificationResponse
            {
                ClientId = clientId,
                KycStatus = "Approved",
                AmlStatus = "Cleared",
                KycApprovedAt = kycApprovedAt
            });

        var service = CreateService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KycExpiredException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));

        Assert.Equal(clientId, exception.ClientId);
        Assert.Equal(kycApprovedAt, exception.KycApprovedAt);
        Assert.Equal(kycApprovedAt.AddMonths(12), exception.ExpiryDate);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithKycApprovedExactly12MonthsAgo_ThrowsKycExpiredException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);
        var kycApprovedAt = DateTime.UtcNow.AddMonths(-12).AddSeconds(-1); // Just over 12 months

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientVerificationResponse
            {
                ClientId = clientId,
                KycStatus = "Approved",
                AmlStatus = "Cleared",
                KycApprovedAt = kycApprovedAt
            });

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<KycExpiredException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateApplicationAsync_WithKycApprovedLessThan12MonthsAgo_AllowsLoanCreation()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var productCode = "TEST_PRODUCT";
        var request = CreateValidLoanRequest(clientId, productCode);
        var kycApprovedAt = DateTime.UtcNow.AddMonths(-6); // 6 months ago

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientVerificationResponse
            {
                ClientId = clientId,
                KycStatus = "Approved",
                AmlStatus = "Cleared",
                KycApprovedAt = kycApprovedAt
            });

        SetupSuccessfulLoanCreation(clientId, productCode);

        var service = CreateService();

        // Act
        var result = await service.CreateApplicationAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LoanNumber); // Verify loan number was generated
        _clientManagementClientMock.Verify(
            x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithClientNotFound_ThrowsKycNotVerifiedException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KycNotVerifiedException(clientId, "NotFound"));

        var service = CreateService();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KycNotVerifiedException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));

        Assert.Equal(clientId, exception.ClientId);
        Assert.Equal("NotFound", exception.KycStatus);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithoutClientManagementClient_SkipsKycValidation()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var productCode = "TEST_PRODUCT";
        var request = CreateValidLoanRequest(clientId, productCode);

        SetupSuccessfulLoanCreation(clientId, productCode);

        // Create service without ClientManagementClient
        var service = new LoanApplicationService(
            _loggerMock.Object,
            _productServiceMock.Object,
            _creditAssessmentServiceMock.Object,
            _workflowServiceMock.Object,
            _complianceServiceMock.Object,
            _applicationRepositoryMock.Object,
            _versioningServiceMock.Object,
            clientManagementClient: null);

        // Act
        var result = await service.CreateApplicationAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _clientManagementClientMock.Verify(
            x => x.GetClientVerificationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateApplicationAsync_WithClientManagementServiceException_PropagatesException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var request = CreateValidLoanRequest(clientId);

        _clientManagementClientMock
            .Setup(x => x.GetClientVerificationAsync(clientId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClientManagementServiceException(
                "Unable to verify KYC status. The Client Management Service is unreachable."));

        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => service.CreateApplicationAsync(request, CancellationToken.None));
    }

    private LoanApplicationService CreateService()
    {
        return new LoanApplicationService(
            _loggerMock.Object,
            _productServiceMock.Object,
            _creditAssessmentServiceMock.Object,
            _workflowServiceMock.Object,
            _complianceServiceMock.Object,
            _applicationRepositoryMock.Object,
            _versioningServiceMock.Object,
            _clientManagementClientMock.Object);
    }

    private CreateLoanApplicationRequest CreateValidLoanRequest(Guid clientId, string productCode = "TEST_PRODUCT")
    {
        return new CreateLoanApplicationRequest
        {
            ClientId = clientId,
            ProductCode = productCode,
            RequestedAmount = 10000m,
            TermMonths = 12,
            ApplicationData = new Dictionary<string, object>()
        };
    }

    private void SetupSuccessfulLoanCreation(Guid clientId, string productCode)
    {
        _productServiceMock
            .Setup(x => x.GetProductAsync(productCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoanProduct
            {
                Code = productCode,
                Name = "Test Product",
                IsActive = true,
                MinAmount = 1000m,
                MaxAmount = 100000m,
                MinTermMonths = 6,
                MaxTermMonths = 60
            });

        _versioningServiceMock
            .Setup(x => x.GenerateLoanNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("LUS-2024-000001");

        _productServiceMock
            .Setup(x => x.ValidateApplicationForProductAsync(
                It.IsAny<LoanProduct>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleEngineResult { IsValid = true });

        _applicationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<IntelliFin.Shared.DomainModels.Entities.LoanApplication>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntelliFin.Shared.DomainModels.Entities.LoanApplication app, CancellationToken ct) => app);

        _workflowServiceMock
            .Setup(x => x.GetWorkflowStepsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowStep>());

        _complianceServiceMock
            .Setup(x => x.GetRequiredDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
    }
}
