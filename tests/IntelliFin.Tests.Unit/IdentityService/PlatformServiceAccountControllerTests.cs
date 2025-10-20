using System.Net;
using IntelliFin.IdentityService.Controllers.Platform;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Tests.Unit.IdentityService;

public class PlatformServiceAccountControllerTests
{
    private readonly Mock<IServiceAccountService> _serviceMock = new();
    private readonly Mock<ILogger<PlatformServiceAccountController>> _loggerMock = new();
    private readonly PlatformServiceAccountController _sut;

    public PlatformServiceAccountControllerTests()
    {
        _sut = new PlatformServiceAccountController(
            _serviceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateServiceAccountAsync_WithValidRequest_Returns201Created()
    {
        // Arrange
        var request = new ServiceAccountCreateRequest
        {
            Name = "Test Service",
            Description = "Test Description",
            Scopes = new[] { "test:read", "test:write" }
        };

        var expectedResult = new ServiceAccountDto
        {
            Id = Guid.NewGuid(),
            ClientId = "test-service-abc123",
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            Scopes = request.Scopes,
            Credential = new ServiceCredentialDto
            {
                Id = Guid.NewGuid(),
                Secret = new string('x', 48),
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        _serviceMock
            .Setup(x => x.CreateServiceAccountAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.CreateServiceAccountAsync(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be((int)HttpStatusCode.Created);
        createdResult.Value.Should().BeEquivalentTo(expectedResult);
        createdResult.ActionName.Should().Be(nameof(PlatformServiceAccountController.GetServiceAccountAsync));
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(expectedResult.Id);

        _serviceMock.Verify(
            x => x.CreateServiceAccountAsync(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateServiceAccountAsync_WithInvalidOperation_Returns400BadRequest()
    {
        // Arrange
        var request = new ServiceAccountCreateRequest
        {
            Name = "Test Service"
        };

        _serviceMock
            .Setup(x => x.CreateServiceAccountAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unable to generate unique client identifier"));

        // Act
        var result = await _sut.CreateServiceAccountAsync(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        
        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Service account creation failed");
        problemDetails.Detail.Should().Contain("Unable to generate unique client identifier");
    }

    [Fact]
    public async Task CreateServiceAccountAsync_WithUnexpectedException_Returns500InternalServerError()
    {
        // Arrange
        var request = new ServiceAccountCreateRequest
        {
            Name = "Test Service"
        };

        _serviceMock
            .Setup(x => x.CreateServiceAccountAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _sut.CreateServiceAccountAsync(request, CancellationToken.None);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        
        var problemDetails = statusResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Service account creation failed");
    }

    [Fact]
    public async Task RotateSecretAsync_WithValidId_Returns200Ok()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var expectedResult = new ServiceCredentialDto
        {
            Id = Guid.NewGuid(),
            ServiceAccountId = accountId,
            ClientId = "test-service-abc123",
            Secret = new string('y', 48),
            CreatedAtUtc = DateTime.UtcNow
        };

        _serviceMock
            .Setup(x => x.RotateSecretAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.RotateSecretAsync(accountId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        okResult.Value.Should().BeEquivalentTo(expectedResult);

        _serviceMock.Verify(
            x => x.RotateSecretAsync(accountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RotateSecretAsync_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.RotateSecretAsync(accountId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Service account {accountId} not found"));

        // Act
        var result = await _sut.RotateSecretAsync(accountId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        
        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Service account not found");
    }

    [Fact]
    public async Task RotateSecretAsync_WithInactiveAccount_Returns400BadRequest()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.RotateSecretAsync(accountId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot rotate credentials for an inactive service account"));

        // Act
        var result = await _sut.RotateSecretAsync(accountId, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        
        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Secret rotation failed");
        problemDetails.Detail.Should().Contain("inactive");
    }

    [Fact]
    public async Task RevokeServiceAccountAsync_WithValidId_Returns204NoContent()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.RevokeServiceAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RevokeServiceAccountAsync(accountId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>()
            .Which.StatusCode.Should().Be((int)HttpStatusCode.NoContent);

        _serviceMock.Verify(
            x => x.RevokeServiceAccountAsync(accountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeServiceAccountAsync_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.RevokeServiceAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Service account {accountId} not found"));

        // Act
        var result = await _sut.RevokeServiceAccountAsync(accountId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        
        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Service account not found");
    }

    [Fact]
    public async Task GetServiceAccountAsync_WithValidId_Returns200Ok()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var expectedResult = new ServiceAccountDto
        {
            Id = accountId,
            ClientId = "test-service-abc123",
            Name = "Test Service",
            Description = "Test Description",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            Scopes = new[] { "test:read", "test:write" },
            Credential = null // Never returned in GET
        };

        _serviceMock
            .Setup(x => x.GetServiceAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.GetServiceAccountAsync(accountId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        okResult.Value.Should().BeEquivalentTo(expectedResult);
        
        var dto = okResult.Value.Should().BeOfType<ServiceAccountDto>().Subject;
        dto.Credential.Should().BeNull("credentials should never be returned in GET requests");

        _serviceMock.Verify(
            x => x.GetServiceAccountAsync(accountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetServiceAccountAsync_WithNonExistentId_Returns404NotFound()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.GetServiceAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException($"Service account {accountId} not found"));

        // Act
        var result = await _sut.GetServiceAccountAsync(accountId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        
        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Service account not found");
    }

    [Fact]
    public async Task ListServiceAccountsAsync_WithoutFilter_ReturnsAllAccounts()
    {
        // Arrange
        var expectedResults = new List<ServiceAccountDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientId = "service-1",
                Name = "Service 1",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                Scopes = new[] { "test:read" }
            },
            new()
            {
                Id = Guid.NewGuid(),
                ClientId = "service-2",
                Name = "Service 2",
                IsActive = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                Scopes = new[] { "test:write" }
            }
        };

        _serviceMock
            .Setup(x => x.ListServiceAccountsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _sut.ListServiceAccountsAsync(null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ServiceAccountDto>>().Subject;
        dtos.Should().HaveCount(2);
        dtos.Should().BeEquivalentTo(expectedResults);

        _serviceMock.Verify(
            x => x.ListServiceAccountsAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListServiceAccountsAsync_WithActiveFilter_ReturnsFilteredAccounts()
    {
        // Arrange
        var expectedResults = new List<ServiceAccountDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientId = "active-service",
                Name = "Active Service",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                Scopes = new[] { "test:read" }
            }
        };

        _serviceMock
            .Setup(x => x.ListServiceAccountsAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _sut.ListServiceAccountsAsync(true, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ServiceAccountDto>>().Subject;
        dtos.Should().HaveCount(1);
        dtos.Should().OnlyContain(dto => dto.IsActive);

        _serviceMock.Verify(
            x => x.ListServiceAccountsAsync(true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListServiceAccountsAsync_WithUnexpectedException_Returns500InternalServerError()
    {
        // Arrange
        _serviceMock
            .Setup(x => x.ListServiceAccountsAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _sut.ListServiceAccountsAsync(null, CancellationToken.None);

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        
        var problemDetails = statusResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Service account listing failed");
    }
}
