using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Domain.Exceptions;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for dual-control verification workflow
/// Tests critical BoZ compliance requirement: different officer must verify documents
/// </summary>
public class DocumentDualControlTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private DocumentLifecycleService? _service;
    private Mock<IKycDocumentServiceClient>? _mockKycDocumentClient;
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

        // Create a test client
        var testClient = new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "123456/78/9",
            FirstName = "Test",
            LastName = "User",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977123456",
            PhysicalAddress = "123 Test St",
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
        var logger = loggerFactory.CreateLogger<DocumentLifecycleService>();

        _mockKycDocumentClient = new Mock<IKycDocumentServiceClient>();
        _mockAuditService = new Mock<IAuditService>();

        _service = new DocumentLifecycleService(
            _context,
            _mockKycDocumentClient.Object,
            _mockAuditService.Object,
            logger);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task UploadDocumentAsync_ShouldSetStatusToUploaded()
    {
        // Arrange
        var file = CreateMockPdfFile("test.pdf", 1024);
        var documentId = Guid.NewGuid();

        _mockKycDocumentClient!
            .Setup(x => x.UploadDocumentAsync(It.IsAny<Refit.StreamPart>()))
            .ReturnsAsync(new UploadDocumentResponse
            {
                DocumentId = documentId,
                ObjectKey = $"clients/{_testClientId}/doc-{documentId}.pdf",
                BucketName = "kyc-documents",
                FileHashSha256 = "abc123",
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            });

        // Act
        var result = await _service!.UploadDocumentAsync(
            _testClientId, file, "NRC", "KYC", "user-upload");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.UploadStatus.Should().Be("Uploaded");
        result.Value.UploadedBy.Should().Be("user-upload");
        result.Value.VerifiedBy.Should().BeNull();
        result.Value.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyDocumentAsync_DifferentUser_ShouldVerifySuccessfully()
    {
        // Arrange - Upload document as user1
        var documentId = await CreateTestDocument("user1");

        // Act - Verify as user2 (different user)
        var verifyRequest = new VerifyDocumentRequest
        {
            Approved = true
        };

        var result = await _service!.VerifyDocumentAsync(
            _testClientId, documentId, verifyRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UploadStatus.Should().Be("Verified");
        result.Value.VerifiedBy.Should().Be("user2");
        result.Value.VerifiedAt.Should().NotBeNull();
        result.Value.VerifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify audit event logged
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentVerified",
                "ClientDocument",
                documentId.ToString(),
                "user2",
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyDocumentAsync_SameUser_ShouldThrowDualControlViolationException()
    {
        // Arrange - Upload document as user1
        var documentId = await CreateTestDocument("user1");

        // Act & Assert - Attempt to verify as same user1
        var verifyRequest = new VerifyDocumentRequest
        {
            Approved = true
        };

        var exception = await Assert.ThrowsAsync<DualControlViolationException>(
            () => _service!.VerifyDocumentAsync(_testClientId, documentId, verifyRequest, "user1"));

        exception.UserId.Should().Be("user1");
        exception.UploadedBy.Should().Be("user1");
        exception.DocumentId.Should().Be(documentId);
        exception.Message.Should().Contain("Dual-control violation");
        exception.Message.Should().Contain("cannot verify document");

        // Verify document status NOT changed
        var document = await _context!.ClientDocuments.FindAsync(documentId);
        document!.UploadStatus.Should().Be(UploadStatus.Uploaded);
        document.VerifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task VerifyDocumentAsync_Rejection_ShouldSetRejectedStatus()
    {
        // Arrange - Upload document as user1
        var documentId = await CreateTestDocument("user1");

        // Act - Reject as user2
        var verifyRequest = new VerifyDocumentRequest
        {
            Approved = false,
            RejectionReason = "Photo is unclear, please re-upload"
        };

        var result = await _service!.VerifyDocumentAsync(
            _testClientId, documentId, verifyRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UploadStatus.Should().Be("Rejected");
        result.Value.VerifiedBy.Should().Be("user2"); // Who rejected
        result.Value.VerifiedAt.Should().NotBeNull();

        // Verify rejection reason stored
        var document = await _context!.ClientDocuments.FindAsync(documentId);
        document!.RejectionReason.Should().Be("Photo is unclear, please re-upload");

        // Verify audit event logged
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentRejected",
                "ClientDocument",
                documentId.ToString(),
                "user2",
                It.Is<object>(data => data.ToString()!.Contains("RejectionReason"))),
            Times.Once);
    }

    [Fact]
    public async Task VerifyDocumentAsync_AlreadyVerified_ShouldFail()
    {
        // Arrange - Upload and verify document
        var documentId = await CreateTestDocument("user1");
        var verifyRequest = new VerifyDocumentRequest { Approved = true };
        await _service!.VerifyDocumentAsync(_testClientId, documentId, verifyRequest, "user2");

        // Act - Attempt to verify again
        var result = await _service.VerifyDocumentAsync(
            _testClientId, documentId, verifyRequest, "user3");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be verified");
        result.Error.Should().Contain("Verified status");
    }

    [Fact]
    public async Task VerifyDocumentAsync_AlreadyRejected_ShouldFail()
    {
        // Arrange - Upload and reject document
        var documentId = await CreateTestDocument("user1");
        var rejectRequest = new VerifyDocumentRequest
        {
            Approved = false,
            RejectionReason = "Document expired"
        };
        await _service!.VerifyDocumentAsync(_testClientId, documentId, rejectRequest, "user2");

        // Act - Attempt to verify a rejected document
        var verifyRequest = new VerifyDocumentRequest { Approved = true };
        var result = await _service.VerifyDocumentAsync(
            _testClientId, documentId, verifyRequest, "user3");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be verified");
        result.Error.Should().Contain("Rejected status");
    }

    [Fact]
    public async Task DatabaseConstraint_SelfVerification_ShouldFail()
    {
        // Arrange - Create document via direct SQL (bypassing service layer)
        var documentId = Guid.NewGuid();
        var userId = "same-user";

        // Act & Assert - Attempt to insert with VerifiedBy = UploadedBy
        var exception = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var document = new ClientDocument
            {
                Id = documentId,
                ClientId = _testClientId,
                DocumentType = "NRC",
                Category = "KYC",
                ObjectKey = "test/path",
                BucketName = "bucket",
                FileName = "test.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1024,
                FileHashSha256 = "hash",
                UploadStatus = UploadStatus.Verified,
                UploadedBy = userId,
                UploadedAt = DateTime.UtcNow,
                VerifiedBy = userId, // SAME as UploadedBy - should violate constraint
                VerifiedAt = DateTime.UtcNow,
                RetentionUntil = DateTime.UtcNow.AddYears(7),
                CreatedAt = DateTime.UtcNow
            };

            _context!.ClientDocuments.Add(document);
            await _context.SaveChangesAsync();
        });

        // Verify it's a constraint violation
        exception.InnerException.Should().BeOfType<SqlException>();
        var sqlException = (SqlException)exception.InnerException!;
        sqlException.Message.Should().Contain("CK_ClientDocuments_DualControl");
    }

    [Fact]
    public async Task DatabaseConstraint_DifferentUsers_ShouldSucceed()
    {
        // Arrange - Create document with different uploader and verifier
        var documentId = Guid.NewGuid();

        var document = new ClientDocument
        {
            Id = documentId,
            ClientId = _testClientId,
            DocumentType = "NRC",
            Category = "KYC",
            ObjectKey = "test/path",
            BucketName = "bucket",
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "hash",
            UploadStatus = UploadStatus.Verified,
            UploadedBy = "user1",
            UploadedAt = DateTime.UtcNow,
            VerifiedBy = "user2", // DIFFERENT from UploadedBy - should succeed
            VerifiedAt = DateTime.UtcNow,
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        _context!.ClientDocuments.Add(document);
        await _context.SaveChangesAsync();

        // Assert - No exception thrown
        var savedDoc = await _context.ClientDocuments.FindAsync(documentId);
        savedDoc.Should().NotBeNull();
        savedDoc!.VerifiedBy.Should().Be("user2");
    }

    [Fact]
    public async Task VerifyDocumentAsync_AuditTrail_ShouldLogBothActions()
    {
        // Arrange - Upload document
        var file = CreateMockPdfFile("test.pdf", 1024);
        var documentId = Guid.NewGuid();

        _mockKycDocumentClient!
            .Setup(x => x.UploadDocumentAsync(It.IsAny<Refit.StreamPart>()))
            .ReturnsAsync(new UploadDocumentResponse
            {
                DocumentId = documentId,
                ObjectKey = $"clients/{_testClientId}/doc-{documentId}.pdf",
                BucketName = "kyc-documents",
                FileHashSha256 = "abc123",
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            });

        // Act 1 - Upload as user1
        await _service!.UploadDocumentAsync(_testClientId, file, "NRC", "KYC", "user1");

        // Verify DocumentUploaded audit event
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentUploaded",
                "ClientDocument",
                documentId.ToString(),
                "user1",
                It.IsAny<object>()),
            Times.Once);

        // Act 2 - Verify as user2
        var verifyRequest = new VerifyDocumentRequest { Approved = true };
        await _service.VerifyDocumentAsync(_testClientId, documentId, verifyRequest, "user2");

        // Verify DocumentVerified audit event
        _mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                "DocumentVerified",
                "ClientDocument",
                documentId.ToString(),
                "user2",
                It.IsAny<object>()),
            Times.Once);

        // Assert - Two distinct audit events logged
        _mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                It.IsAny<string>(),
                "ClientDocument",
                documentId.ToString(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task VerifyDocumentAsync_NonExistentDocument_ShouldFail()
    {
        // Arrange
        var nonExistentDocId = Guid.NewGuid();
        var verifyRequest = new VerifyDocumentRequest { Approved = true };

        // Act
        var result = await _service!.VerifyDocumentAsync(
            _testClientId, nonExistentDocId, verifyRequest, "user2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task VerifyDocumentAsync_WrongClient_ShouldFail()
    {
        // Arrange - Upload document for testClient
        var documentId = await CreateTestDocument("user1");

        // Act - Try to verify with wrong client ID
        var wrongClientId = Guid.NewGuid();
        var verifyRequest = new VerifyDocumentRequest { Approved = true };
        var result = await _service!.VerifyDocumentAsync(
            wrongClientId, documentId, verifyRequest, "user2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CompleteDualControlFlow_ShouldSucceed()
    {
        // This test demonstrates the complete dual-control workflow

        // Step 1: Upload document as Officer A
        var file = CreateMockPdfFile("nrc.pdf", 2048);
        var documentId = Guid.NewGuid();

        _mockKycDocumentClient!
            .Setup(x => x.UploadDocumentAsync(It.IsAny<Refit.StreamPart>()))
            .ReturnsAsync(new UploadDocumentResponse
            {
                DocumentId = documentId,
                ObjectKey = $"clients/{_testClientId}/nrc-{documentId}.pdf",
                BucketName = "kyc-documents",
                FileHashSha256 = "def456",
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            });

        var uploadResult = await _service!.UploadDocumentAsync(
            _testClientId, file, "NRC", "KYC", "officer-a");

        uploadResult.IsSuccess.Should().BeTrue();
        uploadResult.Value!.UploadStatus.Should().Be("Uploaded");
        uploadResult.Value.UploadedBy.Should().Be("officer-a");

        // Step 2: Verify document as Officer B (different officer)
        var verifyRequest = new VerifyDocumentRequest { Approved = true };
        var verifyResult = await _service.VerifyDocumentAsync(
            _testClientId, documentId, verifyRequest, "officer-b");

        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value!.UploadStatus.Should().Be("Verified");
        verifyResult.Value.VerifiedBy.Should().Be("officer-b");

        // Step 3: Verify database state
        var document = await _context!.ClientDocuments.FindAsync(documentId);
        document.Should().NotBeNull();
        document!.UploadedBy.Should().Be("officer-a");
        document.VerifiedBy.Should().Be("officer-b");
        document.UploadStatus.Should().Be(UploadStatus.Verified);

        // Step 4: Verify audit trail has both events
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentUploaded",
                "ClientDocument",
                documentId.ToString(),
                "officer-a",
                It.IsAny<object>()),
            Times.Once,
            "Upload event should be logged");

        _mockAuditService.Verify(
            x => x.LogAuditEventAsync(
                "DocumentVerified",
                "ClientDocument",
                documentId.ToString(),
                "officer-b",
                It.IsAny<object>()),
            Times.Once,
            "Verification event should be logged");
    }

    [Fact]
    public async Task RejectionWorkflow_ShouldCaptureReasonAndRejecter()
    {
        // Arrange - Upload document
        var documentId = await CreateTestDocument("uploader");

        // Act - Reject with reason
        var rejectRequest = new VerifyDocumentRequest
        {
            Approved = false,
            RejectionReason = "NRC photo is blurry and unreadable"
        };

        var result = await _service!.VerifyDocumentAsync(
            _testClientId, documentId, rejectRequest, "verifier");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.UploadStatus.Should().Be("Rejected");
        result.Value.VerifiedBy.Should().Be("verifier");

        // Verify database state
        var document = await _context!.ClientDocuments.FindAsync(documentId);
        document!.UploadStatus.Should().Be(UploadStatus.Rejected);
        document.RejectionReason.Should().Be("NRC photo is blurry and unreadable");
        document.VerifiedBy.Should().Be("verifier");

        // Verify audit event
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentRejected",
                "ClientDocument",
                documentId.ToString(),
                "verifier",
                It.IsAny<object>()),
            Times.Once);
    }

    /// <summary>
    /// Helper method to create a test document in Uploaded status
    /// </summary>
    private async Task<Guid> CreateTestDocument(string uploadedBy)
    {
        var documentId = Guid.NewGuid();
        var document = new ClientDocument
        {
            Id = documentId,
            ClientId = _testClientId,
            DocumentType = "NRC",
            Category = "KYC",
            ObjectKey = $"test/path/{documentId}",
            BucketName = "kyc-documents",
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "testhash",
            UploadStatus = UploadStatus.Uploaded,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow
        };

        _context!.ClientDocuments.Add(document);
        await _context.SaveChangesAsync();
        return documentId;
    }

    private static IFormFile CreateMockPdfFile(string fileName, long sizeBytes)
    {
        var mockFile = new Mock<IFormFile>();
        var content = new byte[sizeBytes];
        new Random().NextBytes(content);
        var ms = new MemoryStream(content);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");
        mockFile.Setup(f => f.Length).Returns(sizeBytes);
        mockFile.Setup(f => f.OpenReadStream()).Returns(ms);

        return mockFile.Object;
    }
}
