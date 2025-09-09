using IntelliFin.KycDocumentService.Models;
using IntelliFin.KycDocumentService.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace IntelliFin.KycDocumentService.Tests.Services;

public class KycDocumentServiceTests : IDisposable
{
    private readonly LmsDbContext _context;
    private readonly Mock<IDocumentStorageService> _mockStorageService;
    private readonly Mock<IDocumentValidationService> _mockValidationService;
    private readonly Mock<ILogger<IntelliFin.KycDocumentService.Services.KycDocumentService>> _mockLogger;
    private readonly IntelliFin.KycDocumentService.Services.KycDocumentService _kycService;

    public KycDocumentServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new LmsDbContext(options);
        _mockStorageService = new Mock<IDocumentStorageService>();
        _mockValidationService = new Mock<IDocumentValidationService>();
        _mockLogger = new Mock<ILogger<IntelliFin.KycDocumentService.Services.KycDocumentService>>();

        _kycService = new IntelliFin.KycDocumentService.Services.KycDocumentService(
            _mockStorageService.Object,
            _mockValidationService.Object,
            _context,
            _mockLogger.Object);
    }

    [Fact]
    public async Task UploadDocumentAsync_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        await SeedTestClientAsync(clientId);
        
        var request = CreateTestUploadRequest(clientId.ToString());
        var uploadedBy = "test-user";

        _mockValidationService
            .Setup(x => x.IsDocumentTypeValidAsync(It.IsAny<KycDocumentType>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockStorageService
            .Setup(x => x.SaveDocumentAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test/path/document.jpg");

        _mockValidationService
            .Setup(x => x.ValidateDocumentAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<KycDocumentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentValidationResult
            {
                IsValid = true,
                ConfidenceScore = 0.9f,
                RequiresManualReview = false,
                ProcessorUsed = "TestProcessor"
            });

        // Act
        var result = await _kycService.UploadDocumentAsync(request, uploadedBy);

        // Assert
        result.Should().NotBeNull();
        result.DocumentId.Should().NotBeEmpty();
        result.FileName.Should().Be("test-document.jpg");
        result.Status.Should().Be(KycDocumentStatus.Approved);
        result.RequiresManualReview.Should().BeFalse();

        // Verify database record created
        var verification = await _context.DocumentVerifications.FirstOrDefaultAsync();
        verification.Should().NotBeNull();
        verification!.ClientId.Should().Be(clientId);
        verification.DocumentType.Should().Be(KycDocumentType.NationalId.ToString());
        verification.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task UploadDocumentAsync_WithLowConfidenceScore_ShouldRequireManualReview()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        await SeedTestClientAsync(clientId);
        
        var request = CreateTestUploadRequest(clientId.ToString());
        var uploadedBy = "test-user";

        _mockValidationService
            .Setup(x => x.IsDocumentTypeValidAsync(It.IsAny<KycDocumentType>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockStorageService
            .Setup(x => x.SaveDocumentAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test/path/document.jpg");

        _mockValidationService
            .Setup(x => x.ValidateDocumentAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<KycDocumentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentValidationResult
            {
                IsValid = true,
                ConfidenceScore = 0.6f,
                RequiresManualReview = true,
                ProcessorUsed = "TestProcessor"
            });

        // Act
        var result = await _kycService.UploadDocumentAsync(request, uploadedBy);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(KycDocumentStatus.PendingReview);
        result.RequiresManualReview.Should().BeTrue();

        // Verify database record
        var verification = await _context.DocumentVerifications.FirstOrDefaultAsync();
        verification.Should().NotBeNull();
        verification!.IsVerified.Should().BeFalse();
        verification.VerificationDate.Should().BeNull();
    }

    [Fact]
    public async Task UploadDocumentAsync_WithNonExistentClient_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var request = CreateTestUploadRequest(Guid.NewGuid().ToString());
        var uploadedBy = "test-user";

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _kycService.UploadDocumentAsync(request, uploadedBy));
    }

    [Fact]
    public async Task UploadDocumentAsync_WithInvalidFileType_ShouldThrowArgumentException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        await SeedTestClientAsync(clientId);
        
        var request = CreateTestUploadRequest(clientId.ToString());
        var uploadedBy = "test-user";

        _mockValidationService
            .Setup(x => x.IsDocumentTypeValidAsync(It.IsAny<KycDocumentType>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _kycService.UploadDocumentAsync(request, uploadedBy));
    }

    [Fact]
    public async Task GetDocumentAsync_WithExistingDocument_ShouldReturnDocument()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        
        await SeedTestDocumentVerificationAsync(documentId, clientId);

        // Act
        var result = await _kycService.GetDocumentAsync(documentId.ToString());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(documentId.ToString());
        result.ClientId.Should().Be(clientId.ToString());
        result.DocumentType.Should().Be(KycDocumentType.NationalId);
    }

    [Fact]
    public async Task GetDocumentAsync_WithNonExistentDocument_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act
        var result = await _kycService.GetDocumentAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApproveDocumentAsync_WithExistingDocument_ShouldReturnTrue()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var approvedBy = "compliance-officer";
        var notes = "Document verified successfully";
        
        await SeedTestDocumentVerificationAsync(documentId, clientId);

        // Act
        var result = await _kycService.ApproveDocumentAsync(documentId.ToString(), approvedBy, notes);

        // Assert
        result.Should().BeTrue();

        // Verify database update
        var verification = await _context.DocumentVerifications.FirstAsync();
        verification.IsVerified.Should().BeTrue();
        verification.VerifiedBy.Should().Be(approvedBy);
        verification.VerificationNotes.Should().Be(notes);
        verification.VerificationDate.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectDocumentAsync_WithExistingDocument_ShouldReturnTrue()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var rejectedBy = "compliance-officer";
        var reason = "Document quality is too low";
        
        await SeedTestDocumentVerificationAsync(documentId, clientId);

        // Act
        var result = await _kycService.RejectDocumentAsync(documentId.ToString(), rejectedBy, reason);

        // Assert
        result.Should().BeTrue();

        // Verify database update
        var verification = await _context.DocumentVerifications.FirstAsync();
        verification.IsVerified.Should().BeFalse();
        verification.VerifiedBy.Should().Be(rejectedBy);
        verification.VerificationDecisionReason.Should().Be(reason);
        verification.VerificationDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetClientDocumentsAsync_WithMultipleDocuments_ShouldReturnAllClientDocuments()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var document1Id = Guid.NewGuid();
        var document2Id = Guid.NewGuid();
        var otherClientId = Guid.NewGuid();
        var otherDocumentId = Guid.NewGuid();
        
        await SeedTestDocumentVerificationAsync(document1Id, clientId, KycDocumentType.NationalId);
        await SeedTestDocumentVerificationAsync(document2Id, clientId, KycDocumentType.Passport);
        await SeedTestDocumentVerificationAsync(otherDocumentId, otherClientId, KycDocumentType.DriversLicense);

        // Act
        var result = await _kycService.GetClientDocumentsAsync(clientId.ToString());

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(d => d.Id == document1Id.ToString());
        result.Should().Contain(d => d.Id == document2Id.ToString());
        result.Should().NotContain(d => d.Id == otherDocumentId.ToString());
    }

    [Fact]
    public async Task GenerateComplianceReportAsync_WithTestData_ShouldReturnAccurateReport()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(-30);
        var toDate = DateTime.UtcNow;
        
        var clientId1 = Guid.NewGuid();
        var clientId2 = Guid.NewGuid();
        
        // Add approved document
        await SeedTestDocumentVerificationAsync(Guid.NewGuid(), clientId1, KycDocumentType.NationalId, 
            isVerified: true, verificationDate: DateTime.UtcNow.AddDays(-10));
        
        // Add rejected document
        await SeedTestDocumentVerificationAsync(Guid.NewGuid(), clientId1, KycDocumentType.Passport, 
            isVerified: false, verificationDate: DateTime.UtcNow.AddDays(-5), 
            rejectionReason: "Poor image quality");
        
        // Add pending document
        await SeedTestDocumentVerificationAsync(Guid.NewGuid(), clientId2, KycDocumentType.DriversLicense);

        // Act
        var report = await _kycService.GenerateComplianceReportAsync(fromDate, toDate);

        // Assert
        report.Should().NotBeNull();
        report.TotalDocuments.Should().Be(3);
        report.ApprovedDocuments.Should().Be(1);
        report.RejectedDocuments.Should().Be(1);
        report.PendingDocuments.Should().Be(1);
        report.ComplianceRate.Should().BeApproximately(33.33f, 0.1f);
        
        report.DocumentsByType.Should().ContainKey(KycDocumentType.NationalId);
        report.DocumentsByType.Should().ContainKey(KycDocumentType.Passport);
        report.DocumentsByType.Should().ContainKey(KycDocumentType.DriversLicense);
        
        report.RejectionReasons.Should().ContainKey("Poor image quality");
    }

    [Fact]
    public async Task GetDocumentsForReviewAsync_WithPendingDocuments_ShouldReturnOnlyUnverified()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        // Add verified document
        await SeedTestDocumentVerificationAsync(Guid.NewGuid(), clientId, KycDocumentType.NationalId, 
            isVerified: true, verificationDate: DateTime.UtcNow.AddDays(-1));
        
        // Add unverified documents
        await SeedTestDocumentVerificationAsync(Guid.NewGuid(), clientId, KycDocumentType.Passport);
        await SeedTestDocumentVerificationAsync(Guid.NewGuid(), clientId, KycDocumentType.DriversLicense);

        // Act
        var result = await _kycService.GetDocumentsForReviewAsync(10);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(d => d.Status == KycDocumentStatus.PendingReview);
    }

    private DocumentUploadRequest CreateTestUploadRequest(string clientId)
    {
        var fileContent = "test file content";
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var stream = new MemoryStream(fileBytes);
        
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns("test-document.jpg");
        formFile.Setup(f => f.ContentType).Returns("image/jpeg");
        formFile.Setup(f => f.Length).Returns(fileBytes.Length);
        formFile.Setup(f => f.OpenReadStream()).Returns(stream);

        return new DocumentUploadRequest
        {
            ClientId = clientId,
            DocumentType = KycDocumentType.NationalId,
            File = formFile.Object,
            Description = "Test document upload",
            AutoVerify = true
        };
    }

    private async Task SeedTestClientAsync(Guid clientId)
    {
        var client = new Client
        {
            Id = clientId,
            FirstName = "Test",
            LastName = "Client",
            Email = "test@example.com",
            PhoneNumber = "1234567890",
            NationalId = "123456789",
            DateOfBirth = DateTime.Now.AddYears(-30),
            Address = "Test Address",
            CreatedAt = DateTime.UtcNow
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
    }

    private async Task SeedTestDocumentVerificationAsync(Guid documentId, Guid clientId, 
        KycDocumentType documentType = KycDocumentType.NationalId, bool isVerified = false, 
        DateTime? verificationDate = null, string? rejectionReason = null)
    {
        var verification = new DocumentVerification
        {
            Id = documentId,
            ClientId = clientId,
            DocumentType = documentType.ToString(),
            DocumentNumber = "TEST123",
            DocumentImagePath = $"test/path/{documentId}.jpg",
            IsVerified = isVerified,
            VerificationDate = verificationDate,
            VerificationDecisionReason = rejectionReason ?? "",
            OcrConfidenceScore = 0.8m,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastModified = DateTime.UtcNow
        };

        _context.DocumentVerifications.Add(verification);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

public class DocumentValidationServiceTests
{
    private readonly Mock<IDocumentStorageService> _mockStorageService;
    private readonly Mock<ILogger<DocumentValidationService>> _mockLogger;
    private readonly DocumentValidationService _validationService;

    public DocumentValidationServiceTests()
    {
        _mockStorageService = new Mock<IDocumentStorageService>();
        _mockLogger = new Mock<ILogger<DocumentValidationService>>();
        
        _validationService = new DocumentValidationService(
            _mockStorageService.Object, 
            _mockLogger.Object);
    }

    [Theory]
    [InlineData(KycDocumentType.NationalId, "image/jpeg", true)]
    [InlineData(KycDocumentType.NationalId, "image/png", true)]
    [InlineData(KycDocumentType.NationalId, "application/pdf", true)]
    [InlineData(KycDocumentType.NationalId, "image/gif", false)]
    [InlineData(KycDocumentType.BankStatement, "application/pdf", true)]
    [InlineData(KycDocumentType.BankStatement, "text/plain", false)]
    public async Task IsDocumentTypeValidAsync_WithVariousContentTypes_ShouldReturnCorrectResult(
        KycDocumentType documentType, string contentType, bool expected)
    {
        // Act
        var result = await _validationService.IsDocumentTypeValidAsync(documentType, contentType);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ValidateFileFormatAsync_WithOversizedFile_ShouldReturnError()
    {
        // Arrange
        var largeContent = new byte[11 * 1024 * 1024]; // 11MB
        var stream = new MemoryStream(largeContent);

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(stream, "test.jpg", "image/jpeg");

        // Assert
        errors.Should().HaveCount(1);
        errors[0].Code.Should().Be("FILE_TOO_LARGE");
        errors[0].Severity.Should().Be("Error");
    }

    [Fact]
    public async Task ValidateFileFormatAsync_WithEmptyFile_ShouldReturnError()
    {
        // Arrange
        var stream = new MemoryStream();

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(stream, "test.jpg", "image/jpeg");

        // Assert
        errors.Should().HaveCount(1);
        errors[0].Code.Should().Be("EMPTY_FILE");
        errors[0].Severity.Should().Be("Error");
    }

    [Fact]
    public async Task CalculateConfidenceScoreAsync_WithExpectedFields_ShouldReturnHigherScore()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["id_number"] = "123456789",
            ["full_name"] = "John Doe",
            ["date_of_birth"] = DateTime.Now.AddYears(-30),
            ["expiry_date"] = DateTime.Now.AddYears(5),
            ["text_quality"] = 0.9f
        };

        // Act
        var score = await _validationService.CalculateConfidenceScoreAsync(extractedData, KycDocumentType.NationalId);

        // Assert
        score.Should().BeGreaterThan(0.8f);
        score.Should().BeLessOrEqualTo(1.0f);
    }
}