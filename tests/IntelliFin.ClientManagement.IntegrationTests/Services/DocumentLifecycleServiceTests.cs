using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Integration tests for DocumentLifecycleService
/// Tests document upload, retrieval, and download URL generation
/// </summary>
public class DocumentLifecycleServiceTests : IAsyncLifetime
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
    public async Task UploadDocumentAsync_WithValidPdfFile_ShouldSucceed()
    {
        // Arrange
        var file = CreateMockPdfFile("test-nrc.pdf", 1024 * 1024); // 1MB file
        var documentId = Guid.NewGuid();

        _mockKycDocumentClient!
            .Setup(x => x.UploadDocumentAsync(It.IsAny<Refit.StreamPart>()))
            .ReturnsAsync(new UploadDocumentResponse
            {
                DocumentId = documentId,
                ObjectKey = $"clients/{_testClientId}/nrc-{documentId}.pdf",
                BucketName = "kyc-documents",
                FileHashSha256 = "abc123hash",
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                FileName = file.FileName,
                ContentType = file.ContentType
            });

        // Act
        var result = await _service!.UploadDocumentAsync(
            _testClientId,
            file,
            "NRC",
            "KYC",
            "test-user",
            "test-correlation-id");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DocumentType.Should().Be("NRC");
        result.Value.Category.Should().Be("KYC");
        result.Value.FileName.Should().Be("test-nrc.pdf");
        result.Value.ContentType.Should().Be("application/pdf");

        // Verify document was saved to database
        var document = await _context!.ClientDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        document.Should().NotBeNull();
        document!.ClientId.Should().Be(_testClientId);
        document.UploadStatus.Should().Be(DocumentUploadStatus.Uploaded);
        document.RetentionUntil.Should().BeAfter(DateTime.UtcNow.AddYears(6)); // 7-year retention

        // Verify audit event logged
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentUploaded",
                "ClientDocument",
                documentId.ToString(),
                "test-user",
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadDocumentAsync_WithFileLargerThan10MB_ShouldFail()
    {
        // Arrange
        var file = CreateMockPdfFile("large-file.pdf", 11 * 1024 * 1024); // 11MB file

        // Act
        var result = await _service!.UploadDocumentAsync(
            _testClientId,
            file,
            "NRC",
            "KYC",
            "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exceeds maximum");

        // Verify KycDocumentService was not called
        _mockKycDocumentClient!.Verify(
            x => x.UploadDocumentAsync(It.IsAny<Refit.StreamPart>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadDocumentAsync_WithInvalidContentType_ShouldFail()
    {
        // Arrange
        var file = CreateMockFile("malicious.exe", "application/octet-stream", 1024);

        // Act
        var result = await _service!.UploadDocumentAsync(
            _testClientId,
            file,
            "NRC",
            "KYC",
            "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid content type");
    }

    [Fact]
    public async Task UploadDocumentAsync_WithNonExistentClient_ShouldFail()
    {
        // Arrange
        var file = CreateMockPdfFile("test.pdf", 1024);
        var nonExistentClientId = Guid.NewGuid();

        // Act
        var result = await _service!.UploadDocumentAsync(
            nonExistentClientId,
            file,
            "NRC",
            "KYC",
            "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UploadDocumentAsync_WithInvalidDocumentType_ShouldFail()
    {
        // Arrange
        var file = CreateMockPdfFile("test.pdf", 1024);

        // Act
        var result = await _service!.UploadDocumentAsync(
            _testClientId,
            file,
            "InvalidType",
            "KYC",
            "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid document type");
    }

    [Fact]
    public async Task GetDocumentMetadataAsync_WhenDocumentExists_ShouldReturnMetadata()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new ClientDocument
        {
            Id = documentId,
            ClientId = _testClientId,
            DocumentType = "NRC",
            Category = "KYC",
            ObjectKey = "clients/test/nrc.pdf",
            BucketName = "kyc-documents",
            FileName = "nrc.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "abc123",
            UploadStatus = DocumentUploadStatus.Uploaded,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "test-user",
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow
        };

        _context!.ClientDocuments.Add(document);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service!.GetDocumentMetadataAsync(_testClientId, documentId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(documentId);
        result.Value.DocumentType.Should().Be("NRC");
        result.Value.FileName.Should().Be("nrc.pdf");
    }

    [Fact]
    public async Task GetDocumentMetadataAsync_WhenDocumentNotFound_ShouldFail()
    {
        // Arrange
        var nonExistentDocId = Guid.NewGuid();

        // Act
        var result = await _service!.GetDocumentMetadataAsync(_testClientId, nonExistentDocId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_WhenDocumentExists_ShouldReturnPresignedUrl()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new ClientDocument
        {
            Id = documentId,
            ClientId = _testClientId,
            DocumentType = "NRC",
            Category = "KYC",
            ObjectKey = "clients/test/nrc.pdf",
            BucketName = "kyc-documents",
            FileName = "nrc.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "abc123",
            UploadStatus = DocumentUploadStatus.Uploaded,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "test-user",
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow
        };

        _context!.ClientDocuments.Add(document);
        await _context.SaveChangesAsync();

        var expectedUrl = "https://minio.example.com/presigned-url";
        _mockKycDocumentClient!
            .Setup(x => x.GetDownloadUrlAsync(documentId))
            .ReturnsAsync(new DownloadUrlResponse
            {
                DocumentId = documentId,
                PresignedUrl = expectedUrl,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                FileName = "nrc.pdf"
            });

        // Act
        var result = await _service!.GenerateDownloadUrlAsync(_testClientId, documentId, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PresignedUrl.Should().Be(expectedUrl);
        result.Value.DocumentId.Should().Be(documentId);

        // Verify audit event logged
        _mockAuditService!.Verify(
            x => x.LogAuditEventAsync(
                "DocumentDownloaded",
                "ClientDocument",
                documentId.ToString(),
                "test-user",
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task ListDocumentsAsync_ShouldReturnAllNonArchivedDocuments()
    {
        // Arrange
        var doc1 = new ClientDocument
        {
            Id = Guid.NewGuid(),
            ClientId = _testClientId,
            DocumentType = "NRC",
            Category = "KYC",
            ObjectKey = "path1",
            BucketName = "bucket",
            FileName = "nrc.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "hash1",
            UploadedAt = DateTime.UtcNow.AddDays(-2),
            UploadedBy = "user1",
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            IsArchived = false
        };

        var doc2 = new ClientDocument
        {
            Id = Guid.NewGuid(),
            ClientId = _testClientId,
            DocumentType = "Payslip",
            Category = "KYC",
            ObjectKey = "path2",
            BucketName = "bucket",
            FileName = "payslip.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048,
            FileHashSha256 = "hash2",
            UploadedAt = DateTime.UtcNow.AddDays(-1),
            UploadedBy = "user2",
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            IsArchived = false
        };

        var archivedDoc = new ClientDocument
        {
            Id = Guid.NewGuid(),
            ClientId = _testClientId,
            DocumentType = "Other",
            Category = "General",
            ObjectKey = "path3",
            BucketName = "bucket",
            FileName = "old.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            FileHashSha256 = "hash3",
            UploadedAt = DateTime.UtcNow.AddDays(-10),
            UploadedBy = "user3",
            RetentionUntil = DateTime.UtcNow.AddYears(7),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            IsArchived = true,
            ArchivedAt = DateTime.UtcNow.AddDays(-5)
        };

        _context!.ClientDocuments.AddRange(doc1, doc2, archivedDoc);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service!.ListDocumentsAsync(_testClientId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(2); // Only non-archived documents
        result.Value.Should().Contain(d => d.DocumentType == "NRC");
        result.Value.Should().Contain(d => d.DocumentType == "Payslip");
        result.Value.Should().NotContain(d => d.DocumentType == "Other"); // Archived document excluded

        // Verify ordered by upload date descending (most recent first)
        result.Value[0].UploadedAt.Should().BeAfter(result.Value[1].UploadedAt);
    }

    private static IFormFile CreateMockPdfFile(string fileName, long sizeBytes)
    {
        return CreateMockFile(fileName, "application/pdf", sizeBytes);
    }

    private static IFormFile CreateMockFile(string fileName, string contentType, long sizeBytes)
    {
        var mockFile = new Mock<IFormFile>();
        var content = new byte[sizeBytes];
        new Random().NextBytes(content); // Fill with random data

        var ms = new MemoryStream(content);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(sizeBytes);
        mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream, token));

        return mockFile.Object;
    }
}
