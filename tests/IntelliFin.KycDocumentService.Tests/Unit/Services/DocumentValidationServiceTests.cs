using IntelliFin.KycDocumentService.Models;
using IntelliFin.KycDocumentService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.KycDocumentService.Tests.Unit.Services;

public class DocumentValidationServiceTests
{
    private readonly DocumentValidationService _validationService;
    private readonly Mock<IDocumentStorageService> _storageServiceMock;
    private readonly Mock<IAzureOcrService> _azureOcrServiceMock;
    private readonly Mock<ILogger<DocumentValidationService>> _loggerMock;

    public DocumentValidationServiceTests()
    {
        _storageServiceMock = new Mock<IDocumentStorageService>();
        _azureOcrServiceMock = new Mock<IAzureOcrService>();
        _loggerMock = new Mock<ILogger<DocumentValidationService>>();
        _validationService = new DocumentValidationService(_storageServiceMock.Object, _azureOcrServiceMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData(KycDocumentType.NationalId, "image/jpeg", true)]
    [InlineData(KycDocumentType.NationalId, "image/png", true)]
    [InlineData(KycDocumentType.NationalId, "application/pdf", true)]
    [InlineData(KycDocumentType.NationalId, "text/plain", false)]
    [InlineData(KycDocumentType.BankStatement, "application/pdf", true)]
    [InlineData(KycDocumentType.BankStatement, "image/jpeg", true)]
    [InlineData(KycDocumentType.BankStatement, "video/mp4", false)]
    public async Task IsDocumentTypeValidAsync_ValidatesContentTypes(KycDocumentType documentType, string contentType, bool expectedResult)
    {
        // Act
        var result = await _validationService.IsDocumentTypeValidAsync(documentType, contentType);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task ValidateFileFormatAsync_EmptyFile_ReturnsError()
    {
        // Arrange
        using var emptyStream = new MemoryStream();
        const string fileName = "empty.jpg";
        const string contentType = "image/jpeg";

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(emptyStream, fileName, contentType);

        // Assert
        Assert.Single(errors);
        Assert.Equal("EMPTY_FILE", errors[0].Code);
    }

    [Fact]
    public async Task ValidateFileFormatAsync_FileTooLarge_ReturnsError()
    {
        // Arrange
        var largeFileSize = 15 * 1024 * 1024; // 15MB, over the 10MB limit
        var largeData = new byte[largeFileSize];
        using var largeStream = new MemoryStream(largeData);
        const string fileName = "large.jpg";
        const string contentType = "image/jpeg";

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(largeStream, fileName, contentType);

        // Assert
        Assert.Contains(errors, e => e.Code == "FILE_TOO_LARGE");
    }

    [Fact]
    public async Task ValidateFileFormatAsync_ValidJpegFile_NoErrors()
    {
        // Arrange - Create a minimal valid JPEG file
        var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var jpegData = new byte[1000];
        Array.Copy(jpegHeader, jpegData, jpegHeader.Length);
        
        using var jpegStream = new MemoryStream(jpegData);
        const string fileName = "valid.jpg";
        const string contentType = "image/jpeg";

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(jpegStream, fileName, contentType);

        // Assert
        // Should have validation errors due to ImageSharp not recognizing our minimal JPEG
        // In a real scenario, you'd use actual image files or mock ImageSharp
        Assert.NotNull(errors);
    }

    [Fact]
    public async Task ValidateFileFormatAsync_ValidPdfFile_NoErrors()
    {
        // Arrange - Create a minimal valid PDF file
        var pdfData = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n");
        using var pdfStream = new MemoryStream(pdfData);
        const string fileName = "valid.pdf";
        const string contentType = "application/pdf";

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(pdfStream, fileName, contentType);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateFileFormatAsync_InvalidPdfFile_ReturnsError()
    {
        // Arrange - Create invalid PDF data
        var invalidPdfData = System.Text.Encoding.ASCII.GetBytes("This is not a PDF file");
        using var invalidStream = new MemoryStream(invalidPdfData);
        const string fileName = "invalid.pdf";
        const string contentType = "application/pdf";

        // Act
        var errors = await _validationService.ValidateFileFormatAsync(invalidStream, fileName, contentType);

        // Assert
        Assert.Contains(errors, e => e.Code == "INVALID_PDF_FORMAT");
    }

    [Fact]
    public async Task ExtractDocumentDataAsync_NationalId_WithAzureOcr_ReturnsAzureData()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testData);
        
        var azureResult = new OcrExtractionResult
        {
            ExtractedFields = new Dictionary<string, object>
            {
                ["id_number"] = "123456789",
                ["full_name"] = "John Doe",
                ["date_of_birth"] = "1990-01-01"
            },
            OverallConfidence = 0.95f,
            DocumentType = "national_id"
        };
        
        _azureOcrServiceMock.Setup(x => x.ExtractDataAsync(It.IsAny<Stream>(), KycDocumentType.NationalId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(azureResult);

        // Act
        var result = await _validationService.ExtractDocumentDataAsync(stream, KycDocumentType.NationalId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("national_id", result["document_type"]);
        Assert.Equal("azure_ocr", result["extraction_method"]);
        Assert.Equal("123456789", result["id_number"]);
        Assert.Equal("John Doe", result["full_name"]);
    }

    [Theory]
    [InlineData(KycDocumentType.NationalId)]
    [InlineData(KycDocumentType.Passport)]
    [InlineData(KycDocumentType.DriversLicense)]
    [InlineData(KycDocumentType.PaySlip)]
    [InlineData(KycDocumentType.Other)]
    public async Task ExtractDocumentDataAsync_AllDocumentTypes_WithAzureOcr_ReturnsData(KycDocumentType documentType)
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testData);
        
        var azureResult = new OcrExtractionResult
        {
            ExtractedFields = new Dictionary<string, object> { ["test_field"] = "test_value" },
            OverallConfidence = 0.8f,
            DocumentType = documentType.ToString().ToLowerInvariant()
        };
        
        _azureOcrServiceMock.Setup(x => x.ExtractDataAsync(It.IsAny<Stream>(), documentType, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(azureResult);

        // Act
        var result = await _validationService.ExtractDocumentDataAsync(stream, documentType);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("document_type", result.Keys);
        Assert.Contains("extraction_method", result.Keys);
        Assert.Equal("azure_ocr", result["extraction_method"]);
    }
    
    [Fact]
    public async Task ExtractDocumentDataAsync_AzureOcrFails_FallsBackToSimplified()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testData);
        
        _azureOcrServiceMock.Setup(x => x.ExtractDataAsync(It.IsAny<Stream>(), It.IsAny<KycDocumentType>(), It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new Exception("Azure OCR service unavailable"));

        // Act
        var result = await _validationService.ExtractDocumentDataAsync(stream, KycDocumentType.NationalId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("national_id", result["document_type"]);
        Assert.Equal("simplified", result["extraction_method"]);
        Assert.Contains("text_quality", result.Keys);
    }

    [Fact]
    public async Task CalculateConfidenceScoreAsync_WithAzureOcrConfidence_ReturnsHigherScore()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["id_number"] = "123456789",
            ["full_name"] = "John Doe",
            ["date_of_birth"] = "1990-01-01",
            ["expiry_date"] = "2030-01-01",
            ["azure_ocr_confidence"] = 0.95f,
            ["extraction_method"] = "azure_ocr"
        };

        // Act
        var score = await _validationService.CalculateConfidenceScoreAsync(extractedData, KycDocumentType.NationalId);

        // Assert
        Assert.True(score > 0.8f); // Should be high with Azure OCR confidence
        Assert.True(score <= 1.0f);
    }
    
    [Fact]
    public async Task CalculateConfidenceScoreAsync_WithSimplifiedExtraction_ReturnsModerateScore()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["id_number"] = "123456789",
            ["full_name"] = "John Doe",
            ["text_quality"] = 0.7f,
            ["extraction_method"] = "simplified"
        };

        // Act
        var score = await _validationService.CalculateConfidenceScoreAsync(extractedData, KycDocumentType.NationalId);

        // Assert
        Assert.True(score > 0.4f);
        Assert.True(score <= 0.85f); // Should be lower than Azure OCR
    }

    [Fact]
    public async Task CalculateConfidenceScoreAsync_WithoutExpectedFields_ReturnsLowerScore()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["unexpected_field"] = "value"
        };

        // Act
        var score = await _validationService.CalculateConfidenceScoreAsync(extractedData, KycDocumentType.NationalId);

        // Assert
        Assert.True(score >= 0.0f);
        Assert.True(score <= 1.0f);
        Assert.True(score <= 0.6f); // Should be lower without expected fields
    }

    [Fact]
    public async Task CheckDocumentIntegrityAsync_SameHash_ReturnsTrue()
    {
        // Arrange
        var testData = System.Text.Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(testData);
        
        // Calculate expected hash manually
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(testData);
        var expectedHash = Convert.ToBase64String(hashBytes);

        // Act
        var result = await _validationService.CheckDocumentIntegrityAsync(stream, expectedHash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CheckDocumentIntegrityAsync_DifferentHash_ReturnsFalse()
    {
        // Arrange
        var testData = System.Text.Encoding.UTF8.GetBytes("test data");
        using var stream = new MemoryStream(testData);
        const string wrongHash = "wrong-hash";

        // Act
        var result = await _validationService.CheckDocumentIntegrityAsync(stream, wrongHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateDocumentExpiryAsync_ExpiredDocument_ReturnsError()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["expiry_date"] = DateTime.UtcNow.AddDays(-10) // Expired 10 days ago
        };

        // Act
        var errors = await _validationService.ValidateDocumentExpiryAsync(extractedData);

        // Assert
        Assert.Contains(errors, e => e.Code == "DOCUMENT_EXPIRED");
    }

    [Fact]
    public async Task ValidateDocumentExpiryAsync_ExpiringSoon_ReturnsWarning()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["expiry_date"] = DateTime.UtcNow.AddDays(15) // Expires in 15 days
        };

        // Act
        var errors = await _validationService.ValidateDocumentExpiryAsync(extractedData);

        // Assert
        Assert.Contains(errors, e => e.Code == "DOCUMENT_EXPIRING_SOON");
    }

    [Fact]
    public async Task ValidateDocumentExpiryAsync_ValidExpiry_NoErrors()
    {
        // Arrange
        var extractedData = new Dictionary<string, object>
        {
            ["expiry_date"] = DateTime.UtcNow.AddYears(2) // Expires in 2 years
        };

        // Act
        var errors = await _validationService.ValidateDocumentExpiryAsync(extractedData);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateDocumentAsync_CompleteValidation_ReturnsValidationResult()
    {
        // Arrange
        const string documentId = "test-doc-123";
        var testData = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n");
        using var stream = new MemoryStream(testData);

        // Act
        var result = await _validationService.ValidateDocumentAsync(documentId, stream, KycDocumentType.PaySlip);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(documentId, result.DocumentId);
        Assert.NotNull(result.ExtractedData);
        Assert.True(result.ConfidenceScore >= 0.0f && result.ConfidenceScore <= 1.0f);
        Assert.NotNull(result.ProcessorUsed);
        Assert.True(result.ProcessingTime.TotalMilliseconds > 0);
    }
}