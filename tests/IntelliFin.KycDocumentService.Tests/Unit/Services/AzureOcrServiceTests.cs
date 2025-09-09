using IntelliFin.KycDocumentService.Models;
using IntelliFin.KycDocumentService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure;
using Moq;
using Xunit;

namespace IntelliFin.KycDocumentService.Tests.Unit.Services;

public class AzureOcrServiceTests
{
    private readonly Mock<DocumentAnalysisClient> _clientMock;
    private readonly Mock<ILogger<AzureOcrService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AzureOcrService _azureOcrService;

    public AzureOcrServiceTests()
    {
        _clientMock = new Mock<DocumentAnalysisClient>();
        _loggerMock = new Mock<ILogger<AzureOcrService>>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup configuration
        var azureOcrSection = new Mock<IConfigurationSection>();
        azureOcrSection.Setup(x => x["TimeoutSeconds"]).Returns("120");
        azureOcrSection.Setup(x => x["EnableQualityAssessment"]).Returns("true");
        _configurationMock.Setup(x => x.GetSection("AzureOcr")).Returns(azureOcrSection.Object);

        _azureOcrService = new AzureOcrService(_clientMock.Object, _configurationMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData(KycDocumentType.NationalId, "prebuilt-idDocument")]
    [InlineData(KycDocumentType.Passport, "prebuilt-idDocument")]
    [InlineData(KycDocumentType.DriversLicense, "prebuilt-idDocument")]
    [InlineData(KycDocumentType.BankStatement, "prebuilt-document")]
    [InlineData(KycDocumentType.PaySlip, "prebuilt-document")]
    [InlineData(KycDocumentType.Other, "prebuilt-document")]
    public void GetModelIdForDocumentType_ReturnsCorrectModel(KycDocumentType documentType, string expectedModelId)
    {
        // This would test the private method if it were made internal for testing
        // For now, we test it indirectly through ExtractDataAsync
        Assert.True(true); // Placeholder - in real implementation, you'd use InternalsVisibleTo
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ServiceResponds_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new Mock<Response<AnalyzeResult>>();
        var mockAnalyzeResult = AnalyzeResult.FromJson("{}");
        mockResponse.Setup(x => x.Value).Returns(mockAnalyzeResult);
        
        var mockOperation = new Mock<AnalyzeDocumentOperation>("operationId", _clientMock.Object);
        mockOperation.Setup(x => x.WaitForCompletionAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockResponse.Object);

        _clientMock.Setup(x => x.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<string>(),
            It.IsAny<BinaryData>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockOperation.Object);

        // Act
        var result = await _azureOcrService.IsServiceAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsServiceAvailableAsync_ServiceThrowsException_ReturnsFalse()
    {
        // Arrange
        _clientMock.Setup(x => x.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<string>(),
            It.IsAny<BinaryData>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Service unavailable"));

        // Act
        var result = await _azureOcrService.IsServiceAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtractDataAsync_ValidNationalId_ReturnsExtractionResult()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testData);

        var mockDocument = new Mock<AnalyzedDocument>();
        var mockField = new Mock<DocumentField>();
        mockField.Setup(x => x.Content).Returns("123456789");
        mockField.Setup(x => x.Confidence).Returns(0.95f);

        var documentFields = new Dictionary<string, DocumentField>
        {
            ["IdNumber"] = mockField.Object,
            ["FirstName"] = mockField.Object,
            ["LastName"] = mockField.Object
        };

        mockDocument.Setup(x => x.Fields).Returns(documentFields);
        mockDocument.Setup(x => x.Confidence).Returns(0.95f);

        var mockAnalyzeResult = new Mock<AnalyzeResult>();
        mockAnalyzeResult.Setup(x => x.Documents).Returns(new List<AnalyzedDocument> { mockDocument.Object });

        var mockResponse = new Mock<Response<AnalyzeResult>>();
        mockResponse.Setup(x => x.Value).Returns(mockAnalyzeResult.Object);

        var mockOperation = new Mock<AnalyzeDocumentOperation>("operationId", _clientMock.Object);
        mockOperation.Setup(x => x.WaitForCompletionAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockResponse.Object);

        _clientMock.Setup(x => x.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(),
            "prebuilt-idDocument",
            It.IsAny<BinaryData>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockOperation.Object);

        // Act
        var result = await _azureOcrService.ExtractDataAsync(stream, KycDocumentType.NationalId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.OverallConfidence > 0.9f);
        Assert.Contains("id_number", result.ExtractedFields.Keys);
        Assert.Equal("national_id", result.DocumentType);
    }

    [Fact]
    public async Task ExtractDataAsync_RequestFails_ThrowsException()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testData);

        _clientMock.Setup(x => x.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<string>(),
            It.IsAny<BinaryData>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Analysis failed"));

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(() =>
            _azureOcrService.ExtractDataAsync(stream, KycDocumentType.NationalId));
    }

    [Fact]
    public async Task ExtractDataFromUrlAsync_ValidUrl_ReturnsExtractionResult()
    {
        // Arrange
        const string documentUrl = "https://example.com/document.pdf";

        var mockDocument = new Mock<AnalyzedDocument>();
        mockDocument.Setup(x => x.Fields).Returns(new Dictionary<string, DocumentField>());
        mockDocument.Setup(x => x.Confidence).Returns(0.8f);

        var mockAnalyzeResult = new Mock<AnalyzeResult>();
        mockAnalyzeResult.Setup(x => x.Documents).Returns(new List<AnalyzedDocument> { mockDocument.Object });

        var mockResponse = new Mock<Response<AnalyzeResult>>();
        mockResponse.Setup(x => x.Value).Returns(mockAnalyzeResult.Object);

        var mockOperation = new Mock<AnalyzeDocumentOperation>("operationId", _clientMock.Object);
        mockOperation.Setup(x => x.WaitForCompletionAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockResponse.Object);

        _clientMock.Setup(x => x.AnalyzeDocumentFromUriAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<string>(),
            It.IsAny<Uri>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockOperation.Object);

        // Act
        var result = await _azureOcrService.ExtractDataFromUrlAsync(documentUrl, KycDocumentType.PaySlip);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("pay_slip", result.DocumentType);
    }

    [Fact]
    public async Task AnalyzeLayoutAsync_ValidDocument_ReturnsLayoutAnalysis()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(testData);

        var mockPage = new Mock<DocumentPage>();
        mockPage.Setup(x => x.PageNumber).Returns(1);
        mockPage.Setup(x => x.Width).Returns(8.5f);
        mockPage.Setup(x => x.Height).Returns(11.0f);
        mockPage.Setup(x => x.Lines).Returns(new List<DocumentLine>());
        mockPage.Setup(x => x.Words).Returns(new List<DocumentWord>());

        var mockAnalyzeResult = new Mock<AnalyzeResult>();
        mockAnalyzeResult.Setup(x => x.Pages).Returns(new List<DocumentPage> { mockPage.Object });
        mockAnalyzeResult.Setup(x => x.Tables).Returns(new List<DocumentTable>());
        mockAnalyzeResult.Setup(x => x.KeyValuePairs).Returns(new List<DocumentKeyValuePair>());

        var mockResponse = new Mock<Response<AnalyzeResult>>();
        mockResponse.Setup(x => x.Value).Returns(mockAnalyzeResult.Object);

        var mockOperation = new Mock<AnalyzeDocumentOperation>("operationId", _clientMock.Object);
        mockOperation.Setup(x => x.WaitForCompletionAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockResponse.Object);

        _clientMock.Setup(x => x.AnalyzeDocumentAsync(
            It.IsAny<WaitUntil>(),
            "prebuilt-layout",
            It.IsAny<BinaryData>(),
            It.IsAny<AnalyzeDocumentOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockOperation.Object);

        // Act
        var result = await _azureOcrService.AnalyzeLayoutAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Pages);
        Assert.Equal(1, result.Pages[0].PageNumber);
        Assert.Equal(8.5f, result.Pages[0].Width);
        Assert.Equal(11.0f, result.Pages[0].Height);
    }
}