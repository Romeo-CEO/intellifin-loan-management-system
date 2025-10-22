using FluentAssertions;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using IntelliFin.ClientManagement.Workflows.CamundaWorkers;
using IntelliFin.Shared.KycDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.IntegrationTests.Workflows;

/// <summary>
/// End-to-end integration tests for complete EDD workflow
/// Tests full workflow from report generation through approval/rejection
/// </summary>
public class EddWorkflowEndToEndTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private Mock<IAuditService>? _mockAuditService;
    private Mock<IKycDocumentService>? _mockDocumentService;
    private Guid _testClientId;
    private Guid _testKycStatusId;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        _mockAuditService = new Mock<IAuditService>();
        _mockDocumentService = new Mock<IKycDocumentService>();

        // Setup mock document service to succeed
        _mockDocumentService
            .Setup(x => x.UploadDocumentAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(Common.Result<string>.Success("edd-reports/test/report.txt"));
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    private async Task CreateHighRiskClient()
    {
        // Create client with sanctioned name
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "999999/99/9",
            FirstName = "Sanctioned",
            LastName = "Person",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977999999",
            EmailAddress = "sanctioned@example.com",
            PhysicalAddress = "999 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            Employer = "Test Corp",
            SourceOfFunds = "Business",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context!.Clients.Add(client);
        _testClientId = client.Id;

        // Create KYC status
        var kycStatus = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            CurrentState = KycState.EDD_Required,
            KycStartedAt = DateTime.UtcNow.AddDays(-7),
            HasNrc = true,
            HasProofOfAddress = true,
            HasPayslip = true,
            HasEmploymentLetter = false,
            AmlScreeningComplete = true,
            AmlScreenedAt = DateTime.UtcNow.AddDays(-1),
            AmlScreenedBy = "system-workflow",
            RequiresEdd = true,
            EddReason = "Sanctions",
            EddEscalatedAt = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycStatuses.Add(kycStatus);
        _testKycStatusId = kycStatus.Id;

        // Create AML screenings
        var sanctionsScreening = new AmlScreening
        {
            Id = Guid.NewGuid(),
            KycStatusId = kycStatus.Id,
            ScreeningType = "Sanctions",
            ScreeningProvider = "Manual_v2_Fuzzy",
            ScreenedAt = DateTime.UtcNow.AddDays(-1),
            ScreenedBy = "system-workflow",
            IsMatch = true,
            MatchDetails = "{\"matchedName\":\"Sanctioned Person\",\"confidence\":100}",
            RiskLevel = "High",
            Notes = "Sanctions hit",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var pepScreening = new AmlScreening
        {
            Id = Guid.NewGuid(),
            KycStatusId = kycStatus.Id,
            ScreeningType = "PEP",
            ScreeningProvider = "Manual_v2_Fuzzy_Zambia",
            ScreenedAt = DateTime.UtcNow.AddDays(-1),
            ScreenedBy = "system-workflow",
            IsMatch = false,
            RiskLevel = "Clear",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _context.AmlScreenings.AddRange(sanctionsScreening, pepScreening);

        await _context.SaveChangesAsync();
    }

    #region Complete EDD Approval Path

    [Fact]
    public async Task EddWorkflow_CompleteApprovalPath_Success()
    {
        // Arrange
        await CreateHighRiskClient();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

        // Step 1: Generate EDD Report
        var reportGenerator = new EddReportGenerator(
            _context!,
            loggerFactory.CreateLogger<EddReportGenerator>());

        var reportGenerationWorker = new EddReportGenerationWorker(
            loggerFactory.CreateLogger<EddReportGenerationWorker>(),
            _context,
            reportGenerator,
            _mockDocumentService!.Object);

        var reportJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["kycStatusId"] = _testKycStatusId.ToString()
        }, "io.intellifin.edd.generate-report");

        var reportJobClient = new Mock<IJobClient>();
        var reportVariables = new Dictionary<string, object>();

        reportJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(reportVariables));

        // Act - Step 1: Generate Report
        await reportGenerationWorker.HandleJobAsync(reportJobClient.Object, reportJob.Object);

        // Assert - Step 1
        reportVariables["eddReportGenerated"].Should().Be(true);
        reportVariables.Should().ContainKey("reportObjectKey");
        reportVariables["overallRiskLevel"].Should().Be("High");

        // Verify report stored in KYC status
        var kycAfterReport = await _context!.KycStatuses.FindAsync(_testKycStatusId);
        kycAfterReport!.EddReportObjectKey.Should().NotBeNullOrEmpty();

        // Step 2: Simulate Compliance Approval
        var statusUpdateWorker = new EddStatusUpdateWorker(
            loggerFactory.CreateLogger<EddStatusUpdateWorker>(),
            _context,
            _mockAuditService!.Object);

        // Simulate CEO approval (after compliance approval)
        var approvalJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["kycStatusId"] = _testKycStatusId.ToString(),
            ["complianceApprovedBy"] = "compliance-officer-1",
            ["complianceComments"] = "Reviewed and approved with enhanced monitoring",
            ["ceoApprovedBy"] = "ceo-1",
            ["ceoComments"] = "Approved with restricted services",
            ["riskAcceptanceLevel"] = "RestrictedServices"
        }, "io.intellifin.edd.update-status-approved");

        var approvalJobClient = new Mock<IJobClient>();
        var approvalVariables = new Dictionary<string, object>();

        approvalJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(approvalVariables));

        // Act - Step 2: Approve EDD
        await statusUpdateWorker.HandleJobAsync(approvalJobClient.Object, approvalJob.Object);

        // Assert - Step 2: Final State
        var finalKycStatus = await _context.KycStatuses.FindAsync(_testKycStatusId);
        finalKycStatus!.CurrentState.Should().Be(KycState.Completed);
        finalKycStatus.KycCompletedAt.Should().NotBeNull();
        finalKycStatus.EddApprovedBy.Should().Be("compliance-officer-1");
        finalKycStatus.EddCeoApprovedBy.Should().Be("ceo-1");
        finalKycStatus.EddApprovedAt.Should().NotBeNull();
        finalKycStatus.RiskAcceptanceLevel.Should().Be("RestrictedServices");
        finalKycStatus.ComplianceComments.Should().Contain("enhanced monitoring");
        finalKycStatus.CeoComments.Should().Contain("restricted services");

        // Verify audit event logged
        _mockAuditService.Verify(x => x.LogEventAsync(
            "EDD.Approved",
            "ClientManagement",
            _testClientId.ToString(),
            "ceo-1",
            It.IsAny<object>(),
            It.IsAny<string>()), Times.Once);

        approvalVariables["statusUpdated"].Should().Be(true);
    }

    #endregion

    #region Compliance Rejection Scenario

    [Fact]
    public async Task EddWorkflow_ComplianceRejection_UpdatesStatusCorrectly()
    {
        // Arrange
        await CreateHighRiskClient();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var statusUpdateWorker = new EddStatusUpdateWorker(
            loggerFactory.CreateLogger<EddStatusUpdateWorker>(),
            _context!,
            _mockAuditService!.Object);

        var rejectionJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["kycStatusId"] = _testKycStatusId.ToString(),
            ["rejectedBy"] = "compliance-officer-1",
            ["rejectionStage"] = "Compliance",
            ["rejectionReason"] = "Sanctions hit confirmed, unacceptable risk"
        }, "io.intellifin.edd.update-status-rejected");

        var rejectionJobClient = new Mock<IJobClient>();
        var rejectionVariables = new Dictionary<string, object>();

        rejectionJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(rejectionVariables));

        // Act
        await statusUpdateWorker.HandleJobAsync(rejectionJobClient.Object, rejectionJob.Object);

        // Assert
        var finalKycStatus = await _context!.KycStatuses.FindAsync(_testKycStatusId);
        finalKycStatus!.CurrentState.Should().Be(KycState.Rejected);
        finalKycStatus.ComplianceComments.Should().Contain("REJECTED");
        finalKycStatus.ComplianceComments.Should().Contain("unacceptable risk");

        // Verify audit event logged
        _mockAuditService.Verify(x => x.LogEventAsync(
            "EDD.Rejected",
            "ClientManagement",
            _testClientId.ToString(),
            "compliance-officer-1",
            It.IsAny<object>(),
            It.IsAny<string>()), Times.Once);

        rejectionVariables["statusUpdated"].Should().Be(true);
    }

    #endregion

    #region CEO Rejection Scenario

    [Fact]
    public async Task EddWorkflow_CeoRejection_UpdatesStatusCorrectly()
    {
        // Arrange
        await CreateHighRiskClient();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var statusUpdateWorker = new EddStatusUpdateWorker(
            loggerFactory.CreateLogger<EddStatusUpdateWorker>(),
            _context!,
            _mockAuditService!.Object);

        var rejectionJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["kycStatusId"] = _testKycStatusId.ToString(),
            ["rejectedBy"] = "ceo-1",
            ["rejectionStage"] = "CEO",
            ["rejectionReason"] = "Risk too high despite compliance approval"
        }, "io.intellifin.edd.update-status-rejected");

        var rejectionJobClient = new Mock<IJobClient>();
        var rejectionVariables = new Dictionary<string, object>();

        rejectionJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(rejectionVariables));

        // Act
        await statusUpdateWorker.HandleJobAsync(rejectionJobClient.Object, rejectionJob.Object);

        // Assert
        var finalKycStatus = await _context!.KycStatuses.FindAsync(_testKycStatusId);
        finalKycStatus!.CurrentState.Should().Be(KycState.Rejected);
        finalKycStatus.CeoComments.Should().Contain("REJECTED");
        finalKycStatus.CeoComments.Should().Contain("Risk too high");

        _mockAuditService.Verify(x => x.LogEventAsync(
            "EDD.Rejected",
            "ClientManagement",
            _testClientId.ToString(),
            "ceo-1",
            It.IsAny<object>(),
            It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Report Generation Integration

    [Fact]
    public async Task EddWorkflow_ReportGeneration_CreatesAndStoresReport()
    {
        // Arrange
        await CreateHighRiskClient();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reportGenerator = new EddReportGenerator(
            _context!,
            loggerFactory.CreateLogger<EddReportGenerator>());

        var reportGenerationWorker = new EddReportGenerationWorker(
            loggerFactory.CreateLogger<EddReportGenerationWorker>(),
            _context,
            reportGenerator,
            _mockDocumentService!.Object);

        var reportJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["kycStatusId"] = _testKycStatusId.ToString(),
            ["correlationId"] = "test-correlation-123"
        }, "io.intellifin.edd.generate-report");

        var reportJobClient = new Mock<IJobClient>();
        var reportVariables = new Dictionary<string, object>();

        reportJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(reportVariables));

        // Act
        await reportGenerationWorker.HandleJobAsync(reportJobClient.Object, reportJob.Object);

        // Assert
        reportVariables["eddReportGenerated"].Should().Be(true);
        reportVariables["clientName"].Should().Be("Sanctioned Person");
        reportVariables["overallRiskLevel"].Should().Be("High");
        reportVariables["eddReason"].Should().Be("Sanctions");

        // Verify MinIO upload was called
        _mockDocumentService.Verify(x => x.UploadDocumentAsync(
            It.IsAny<byte[]>(),
            It.Is<string>(s => s.Contains("edd-report")),
            "text/plain",
            "kyc-documents",
            It.Is<string>(s => s.StartsWith("edd-reports/")),
            "test-correlation-123"), Times.Once);

        // Verify KYC status updated
        var kycStatus = await _context!.KycStatuses.FindAsync(_testKycStatusId);
        kycStatus!.EddReportObjectKey.Should().StartWith("edd-reports/");
    }

    #endregion

    #region Different Risk Acceptance Levels

    [Theory]
    [InlineData("Standard")]
    [InlineData("EnhancedMonitoring")]
    [InlineData("RestrictedServices")]
    public async Task EddWorkflow_ApprovalWithDifferentRiskLevels_StoresCorrectly(string riskLevel)
    {
        // Arrange
        await CreateHighRiskClient();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var statusUpdateWorker = new EddStatusUpdateWorker(
            loggerFactory.CreateLogger<EddStatusUpdateWorker>(),
            _context!,
            _mockAuditService!.Object);

        var approvalJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = _testClientId.ToString(),
            ["kycStatusId"] = _testKycStatusId.ToString(),
            ["complianceApprovedBy"] = "compliance-1",
            ["complianceComments"] = "Approved",
            ["ceoApprovedBy"] = "ceo-1",
            ["ceoComments"] = $"Approved with {riskLevel}",
            ["riskAcceptanceLevel"] = riskLevel
        }, "io.intellifin.edd.update-status-approved");

        var approvalJobClient = new Mock<IJobClient>();
        var approvalVariables = new Dictionary<string, object>();

        approvalJobClient.Setup(x => x.NewCompleteJobCommand(It.IsAny<long>()))
            .Returns(new MockCompleteJobCommand(approvalVariables));

        // Act
        await statusUpdateWorker.HandleJobAsync(approvalJobClient.Object, approvalJob.Object);

        // Assert
        var finalKycStatus = await _context!.KycStatuses.FindAsync(_testKycStatusId);
        finalKycStatus!.RiskAcceptanceLevel.Should().Be(riskLevel);
        finalKycStatus.CurrentState.Should().Be(KycState.Completed);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task EddWorkflow_InvalidClientId_ThrowsException()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var reportGenerator = new EddReportGenerator(
            _context!,
            loggerFactory.CreateLogger<EddReportGenerator>());

        var reportGenerationWorker = new EddReportGenerationWorker(
            loggerFactory.CreateLogger<EddReportGenerationWorker>(),
            _context,
            reportGenerator,
            _mockDocumentService!.Object);

        var reportJob = CreateMockJob(new Dictionary<string, object>
        {
            ["clientId"] = Guid.NewGuid().ToString(), // Invalid client ID
            ["kycStatusId"] = Guid.NewGuid().ToString()
        }, "io.intellifin.edd.generate-report");

        var reportJobClient = new Mock<IJobClient>();

        reportJobClient.Setup(x => x.NewFailCommand(It.IsAny<long>()))
            .Returns(new MockFailJobCommand());

        // Act & Assert
        await reportGenerationWorker.HandleJobAsync(reportJobClient.Object, reportJob.Object);

        // Verify job was failed
        reportJobClient.Verify(x => x.NewFailCommand(It.IsAny<long>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static Mock<IJob> CreateMockJob(Dictionary<string, object> variables, string jobType)
    {
        var mockJob = new Mock<IJob>();
        mockJob.Setup(j => j.Key).Returns(123456);
        mockJob.Setup(j => j.Type).Returns(jobType);
        mockJob.Setup(j => j.ProcessInstanceKey).Returns(789);
        mockJob.Setup(j => j.Retries).Returns(3);
        mockJob.Setup(j => j.Variables).Returns(new MockVariables(variables));

        return mockJob;
    }

    private class MockVariables : Dictionary<string, object>
    {
        public MockVariables(Dictionary<string, object> variables) : base(variables)
        {
        }
    }

    private class MockCompleteJobCommand : ICompleteJobCommandStep1
    {
        private readonly Dictionary<string, object> _capturedVariables;

        public MockCompleteJobCommand(Dictionary<string, object> capturedVariables)
        {
            _capturedVariables = capturedVariables;
        }

        public ICompleteJobCommandStep2 Variables(string variables)
        {
            return this;
        }

        public ICompleteJobCommandStep2 Variables(object variables)
        {
            if (variables is Dictionary<string, object> dict)
            {
                foreach (var kvp in dict)
                {
                    _capturedVariables[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        public async Task Send(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }
    }

    private class MockFailJobCommand : IFailJobCommandStep1
    {
        public IFailJobCommandStep2 Retries(int retries)
        {
            return this;
        }

        public IFailJobCommandStep2 ErrorMessage(string errorMsg)
        {
            return this;
        }

        public async Task Send(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
        }
    }

    #endregion
}
