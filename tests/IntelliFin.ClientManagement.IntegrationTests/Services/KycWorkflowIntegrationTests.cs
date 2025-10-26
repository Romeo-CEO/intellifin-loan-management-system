using FluentAssertions;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.BusinessRules;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Domain.Exceptions;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MsSql;
using Xunit;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for KYC workflow and state machine
/// </summary>
public class KycWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private KycWorkflowService? _service;
    private Mock<IAuditService>? _mockAuditService;
    private Guid _testClientId;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        // Create test client
        var testClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "555555/55/5",
            FirstName = "KYC",
            LastName = "TestUser",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977555555",
            PhysicalAddress = "123 KYC St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };

        _context.Clients.Add(testClient);
        await _context.SaveChangesAsync();
        _testClientId = testClient.Id;

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<KycWorkflowService>();

        _mockAuditService = new Mock<IAuditService>();

        _service = new KycWorkflowService(_context, _mockAuditService.Object, logger);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task InitiateKycAsync_ShouldCreateKycStatusWithPendingState()
    {
        // Act
        var result = await _service!.InitiateKycAsync(_testClientId, "user1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ClientId.Should().Be(_testClientId);
        result.Value.CurrentState.Should().Be("Pending");
        result.Value.KycStartedAt.Should().NotBeNull();
        result.Value.KycCompletedAt.Should().BeNull();

        // Verify database
        var kycStatus = await _context!.KycStatuses.FirstOrDefaultAsync(k => k.ClientId == _testClientId);
        kycStatus.Should().NotBeNull();
        kycStatus!.CurrentState.Should().Be(KycState.Pending);

        // Verify audit event
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "KycInitiated",
                "KycStatus",
                It.IsAny<string>(),
                "user1",
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task InitiateKycAsync_WhenAlreadyExists_ShouldReturnFailure()
    {
        // Arrange - Create existing KYC
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Act - Try to initiate again
        var result = await _service.InitiateKycAsync(_testClientId, "user2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task UpdateKycStateAsync_ValidTransition_ShouldSucceed()
    {
        // Arrange - Initiate KYC
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Act - Transition to InProgress
        var updateRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true,
            HasProofOfAddress = true,
            Notes = "Documents being collected"
        };

        var result = await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, updateRequest, "user1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentState.Should().Be("InProgress");
        result.Value.HasNrc.Should().BeTrue();
        result.Value.HasProofOfAddress.Should().BeTrue();

        // Verify audit event
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "KycStateChanged",
                "KycStatus",
                It.IsAny<string>(),
                "user1",
                It.Is<object>(data => data.ToString()!.Contains("InProgress"))),
            Times.Once);
    }

    [Fact]
    public async Task UpdateKycStateAsync_InvalidTransition_ShouldThrowException()
    {
        // Arrange - Initiate KYC (Pending state)
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Act & Assert - Try to go directly from Pending to Completed (invalid)
        var updateRequest = new UpdateKycStateRequest
        {
            NewState = "Completed"
        };

        await Assert.ThrowsAsync<InvalidKycStateTransitionException>(
            () => _service.UpdateKycStateAsync(
                _testClientId, KycState.Completed, updateRequest, "user1"));

        // Verify state NOT changed
        var kycStatus = await _context!.KycStatuses.FirstOrDefaultAsync(k => k.ClientId == _testClientId);
        kycStatus!.CurrentState.Should().Be(KycState.Pending);
    }

    [Fact]
    public async Task CompleteKycWorkflow_HappyPath_ShouldSucceed()
    {
        // Step 1: Initiate KYC
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Step 2: Transition to InProgress with documents
        var inProgressRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true,
            HasProofOfAddress = true,
            HasPayslip = true
        };
        await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, inProgressRequest, "user1");

        // Step 3: Complete AML screening and transition to Completed
        var completeRequest = new UpdateKycStateRequest
        {
            NewState = "Completed",
            AmlScreeningComplete = true,
            AmlScreenedBy = "user2",
            CompletedBy = "user2"
        };

        var result = await _service.UpdateKycStateAsync(
            _testClientId, KycState.Completed, completeRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentState.Should().Be("Completed");
        result.Value.IsDocumentComplete.Should().BeTrue();
        result.Value.AmlScreeningComplete.Should().BeTrue();
        result.Value.KycCompletedAt.Should().NotBeNull();
        result.Value.KycCompletedBy.Should().Be("user2");
    }

    [Fact]
    public async Task UpdateKycStateAsync_ToCompleted_WithoutDocuments_ShouldThrow()
    {
        // Arrange
        await _service!.InitiateKycAsync(_testClientId, "user1");

        var inProgressRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true // Only one document
        };
        await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, inProgressRequest, "user1");

        // Act & Assert - Try to complete without all documents
        var completeRequest = new UpdateKycStateRequest
        {
            NewState = "Completed",
            AmlScreeningComplete = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateKycStateAsync(
                _testClientId, KycState.Completed, completeRequest, "user1"));
    }

    [Fact]
    public async Task UpdateKycStateAsync_ToCompleted_WithoutAmlScreening_ShouldThrow()
    {
        // Arrange - Complete documents but no AML
        await _service!.InitiateKycAsync(_testClientId, "user1");

        var inProgressRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true,
            HasProofOfAddress = true,
            HasPayslip = true
        };
        await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, inProgressRequest, "user1");

        // Act & Assert - Try to complete without AML screening
        var completeRequest = new UpdateKycStateRequest
        {
            NewState = "Completed",
            AmlScreeningComplete = false // AML not complete
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateKycStateAsync(
                _testClientId, KycState.Completed, completeRequest, "user1"));
    }

    [Fact]
    public async Task UpdateKycStateAsync_ToEddRequired_ShouldSetEddFields()
    {
        // Arrange
        await _service!.InitiateKycAsync(_testClientId, "user1");

        var inProgressRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true
        };
        await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, inProgressRequest, "user1");

        // Act - Escalate to EDD
        var eddRequest = new UpdateKycStateRequest
        {
            NewState = "EDD_Required",
            EddReason = "PEP",
            Notes = "Client is a politically exposed person"
        };

        var result = await _service.UpdateKycStateAsync(
            _testClientId, KycState.EDD_Required, eddRequest, "user1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentState.Should().Be("EDD_Required");
        result.Value.RequiresEdd.Should().BeTrue();
        result.Value.EddReason.Should().Be("PEP");
        result.Value.EddEscalatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task KycStateMachine_IsValidTransition_ShouldValidateCorrectly()
    {
        // Valid transitions
        KycStateMachine.IsValidTransition(KycState.Pending, KycState.InProgress).Should().BeTrue();
        KycStateMachine.IsValidTransition(KycState.InProgress, KycState.Completed).Should().BeTrue();
        KycStateMachine.IsValidTransition(KycState.InProgress, KycState.EDD_Required).Should().BeTrue();
        KycStateMachine.IsValidTransition(KycState.InProgress, KycState.Rejected).Should().BeTrue();
        KycStateMachine.IsValidTransition(KycState.EDD_Required, KycState.Completed).Should().BeTrue();
        KycStateMachine.IsValidTransition(KycState.EDD_Required, KycState.Rejected).Should().BeTrue();

        // Invalid transitions
        KycStateMachine.IsValidTransition(KycState.Pending, KycState.Completed).Should().BeFalse();
        KycStateMachine.IsValidTransition(KycState.Pending, KycState.EDD_Required).Should().BeFalse();
        KycStateMachine.IsValidTransition(KycState.Completed, KycState.InProgress).Should().BeFalse();
        KycStateMachine.IsValidTransition(KycState.Rejected, KycState.Completed).Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseConstraint_UniqueClientId_ShouldEnforce()
    {
        // Arrange - Create KYC status
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Act & Assert - Try to create another KYC status for same client
        var duplicateKyc = new KycStatus
        {
            Id = Guid.NewGuid(),
            ClientId = _testClientId, // Same client
            CurrentState = KycState.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context!.KycStatuses.Add(duplicateKyc);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task IsDocumentComplete_ComputedColumn_ShouldCalculateCorrectly()
    {
        // Arrange
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Act - Set documents to make it complete
        var updateRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true,
            HasProofOfAddress = true,
            HasPayslip = true
        };

        var result = await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, updateRequest, "user1");

        // Assert
        result.Value!.IsDocumentComplete.Should().BeTrue();

        // Verify computed column in database
        var kycStatus = await _context!.KycStatuses
            .FirstOrDefaultAsync(k => k.ClientId == _testClientId);
        kycStatus!.IsDocumentComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GetKycStatusAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        await _service!.InitiateKycAsync(_testClientId, "user1");

        // Act
        var result = await _service.GetKycStatusAsync(_testClientId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ClientId.Should().Be(_testClientId);
        result.Value.ClientName.Should().Be("KYC TestUser");
        result.Value.CurrentState.Should().Be("Pending");
    }

    [Fact]
    public async Task GetKycStatusAsync_NonExistent_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentClientId = Guid.NewGuid();

        // Act
        var result = await _service!.GetKycStatusAsync(nonExistentClientId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RejectionFlow_ShouldWork()
    {
        // Arrange
        await _service!.InitiateKycAsync(_testClientId, "user1");

        var inProgressRequest = new UpdateKycStateRequest
        {
            NewState = "InProgress",
            HasNrc = true
        };
        await _service.UpdateKycStateAsync(
            _testClientId, KycState.InProgress, inProgressRequest, "user1");

        // Act - Reject KYC
        var rejectRequest = new UpdateKycStateRequest
        {
            NewState = "Rejected",
            Notes = "Incomplete documentation"
        };

        var result = await _service.UpdateKycStateAsync(
            _testClientId, KycState.Rejected, rejectRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentState.Should().Be("Rejected");

        // Verify terminal state - cannot transition further
        KycStateMachine.IsTerminalState(KycState.Rejected).Should().BeTrue();
    }
}
