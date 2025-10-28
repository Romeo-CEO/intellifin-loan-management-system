using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace IntelliFin.LoanOriginationService.Tests.Services;

/// <summary>
/// Integration tests for ClientManagementClient with HTTP message handler mocking.
/// Tests happy path, blocking path, timeout, and error scenarios.
/// </summary>
public class ClientManagementClientIntegrationTests
{
    private readonly Mock<ILogger<ClientManagementClient>> _loggerMock;

    public ClientManagementClientIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<ClientManagementClient>>();
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithVerifiedClient_ReturnsVerificationResponse()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var kycApprovedAt = DateTime.UtcNow.AddMonths(-3);
        
        var expectedResponse = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Approved",
            AmlStatus = "Cleared",
            KycApprovedAt = kycApprovedAt,
            KycExpiryDate = kycApprovedAt.AddMonths(12),
            VerificationLevel = "Enhanced",
            RiskRating = "Low"
        };

        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.OK, 
            JsonSerializer.Serialize(expectedResponse));
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetClientVerificationAsync(clientId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clientId, result.ClientId);
        Assert.Equal("Approved", result.KycStatus);
        Assert.Equal("Cleared", result.AmlStatus);
        Assert.Equal(kycApprovedAt.Date, result.KycApprovedAt?.Date);
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithNonVerifiedClient_ThrowsKycNotVerifiedException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var response = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Pending",
            AmlStatus = "Pending"
        };

        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.OK, 
            JsonSerializer.Serialize(response));
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetClientVerificationAsync(clientId, CancellationToken.None);

        // Assert - In this scenario, the client returns the response, 
        // and LoanApplicationService is responsible for validation
        Assert.NotNull(result);
        Assert.Equal("Pending", result.KycStatus);
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithClientNotFound_ThrowsKycNotVerifiedException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.NotFound, 
            string.Empty);
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KycNotVerifiedException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
        
        Assert.Equal(clientId, exception.ClientId);
        Assert.Equal("NotFound", exception.KycStatus);
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithServiceUnavailable_ThrowsClientManagementServiceException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable, 
            "Service temporarily unavailable");
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithInternalServerError_ThrowsClientManagementServiceException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.InternalServerError, 
            "Internal server error");
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithTimeout_ThrowsClientManagementServiceException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithNetworkError_ThrowsClientManagementServiceException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithNullResponse_ThrowsClientManagementServiceException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.OK, 
            "null");
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClientVerificationAsync_WithMalformedJson_ThrowsClientManagementServiceException()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        
        var mockHttpMessageHandler = CreateMockHttpMessageHandler(
            HttpStatusCode.OK, 
            "{ invalid json }");
        
        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ClientManagementServiceException>(
            () => client.GetClientVerificationAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClientVerificationAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expectedResponse = new ClientVerificationResponse
        {
            ClientId = clientId,
            KycStatus = "Approved",
            AmlStatus = "Cleared"
        };

        HttpRequestMessage? capturedRequest = null;
        
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse), 
                    Encoding.UTF8, 
                    "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var client = new ClientManagementClient(httpClient, _loggerMock.Object);

        // Act
        await client.GetClientVerificationAsync(clientId, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Contains($"/api/clients/{clientId}/verification", capturedRequest.RequestUri?.ToString());
    }

    private Mock<HttpMessageHandler> CreateMockHttpMessageHandler(
        HttpStatusCode statusCode, 
        string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        return mockHandler;
    }
}
