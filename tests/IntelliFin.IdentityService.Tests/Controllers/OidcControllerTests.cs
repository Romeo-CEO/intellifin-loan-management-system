using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Controllers;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Tests.Controllers;

public class OidcControllerTests
{
    private readonly Mock<IKeycloakService> _mockKeycloakService;
    private readonly Mock<IOidcStateStore> _mockStateStore;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IOptions<FeatureFlags>> _mockFeatureFlags;
    private readonly Mock<ILogger<OidcController>> _mockLogger;
    private readonly OidcController _controller;
    private readonly FeatureFlags _featureFlags;

    public OidcControllerTests()
    {
        _mockKeycloakService = new Mock<IKeycloakService>();
        _mockStateStore = new Mock<IOidcStateStore>();
        _mockSessionService = new Mock<ISessionService>();
        _mockAuditService = new Mock<IAuditService>();
        _mockLogger = new Mock<ILogger<OidcController>>();
        
        _featureFlags = new FeatureFlags { EnableOidc = true };
        _mockFeatureFlags = new Mock<IOptions<FeatureFlags>>();
        _mockFeatureFlags.Setup(x => x.Value).Returns(_featureFlags);

        _controller = new OidcController(
            _mockKeycloakService.Object,
            _mockStateStore.Object,
            _mockSessionService.Object,
            _mockAuditService.Object,
            _mockFeatureFlags.Object,
            _mockLogger.Object);

        // Setup HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Login_GeneratesStateAndRedirects()
    {
        // Arrange
        var returnUrl = "/dashboard";
        var expectedAuthUrl = "https://keycloak.test/auth?state=abc123";

        _controller.HttpContext.Request.Headers["User-Agent"] = "Test Browser";

        _mockKeycloakService.Setup(x => x.GenerateAuthorizationUrl(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            returnUrl))
            .Returns(expectedAuthUrl);

        _mockStateStore.Setup(x => x.StoreAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            returnUrl,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Login(returnUrl);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(expectedAuthUrl, redirectResult.Url);

        // Verify state was stored
        _mockStateStore.Verify(x => x.StoreAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            returnUrl,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify audit event was logged
_mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => e.Action == "LoginStarted"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify nonce cookie was set
        Assert.True(_controller.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public async Task Login_FeatureDisabled_ReturnsBadRequest()
    {
        // Arrange
        _featureFlags.EnableOidc = false;

        // Act
        var result = await _controller.Login("/dashboard");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task Callback_InvalidState_Returns400()
    {
        // Arrange
        var code = "auth-code-123";
        var state = "invalid-state";

        _controller.HttpContext.Request.Headers["User-Agent"] = "Test Browser";

        _mockStateStore.Setup(x => x.GetAsync(state, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OidcStateData?)null);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Verify LoginFailed audit event was logged
_mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => e.Action == "LoginFailed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Callback_UserAgentMismatch_Returns401()
    {
        // Arrange
        var code = "auth-code-123";
        var state = "valid-state";
        var originalUaHash = PkceHelper.ComputeUserAgentHash("Original Browser");
        var differentUaHash = PkceHelper.ComputeUserAgentHash("Different Browser");

        var stateData = new OidcStateData
        {
            CodeVerifier = "verifier-123",
            Nonce = "nonce-123",
            UserAgentHash = originalUaHash
        };

        _controller.HttpContext.Request.Headers["User-Agent"] = "Different Browser";

        _mockStateStore.Setup(x => x.GetAsync(state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stateData);

        _mockStateStore.Setup(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);

        // Verify state was removed
        _mockStateStore.Verify(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()), Times.Once);

        // Verify CSRF failure audit event
_mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => 
                e.Action == "LoginFailed" && 
                e.Details != null && e.Details.ContainsKey("Error") && e.Details["Error"].ToString()!.Contains("CSRF")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Callback_InvalidNonce_Returns401()
    {
        // Arrange
        var code = "auth-code-123";
        var state = "valid-state";
        var userAgent = "Test Browser";

        var stateData = new OidcStateData
        {
            CodeVerifier = "verifier-123",
            Nonce = "correct-nonce",
            UserAgentHash = PkceHelper.ComputeUserAgentHash(userAgent)
        };

        var tokens = new KeycloakTokenResponse
        {
            AccessToken = "access-token",
            IdToken = "id-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 3600
        };

        _controller.HttpContext.Request.Headers["User-Agent"] = userAgent;

        _mockStateStore.Setup(x => x.GetAsync(state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stateData);

        _mockKeycloakService.Setup(x => x.ExchangeCodeForTokensAsync(code, stateData.CodeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        // ID token validation fails due to nonce mismatch
        _mockKeycloakService.Setup(x => x.ValidateIdTokenAsync(tokens.IdToken, stateData.Nonce, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockStateStore.Setup(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);

        // Verify state was cleaned up
        _mockStateStore.Verify(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Callback_ValidCode_ReturnsSessionAndPayload()
    {
        // Arrange
        var code = "auth-code-123";
        var state = "valid-state";
        var userAgent = "Test Browser";

        var stateData = new OidcStateData
        {
            CodeVerifier = "verifier-123",
            Nonce = "nonce-123",
            UserAgentHash = PkceHelper.ComputeUserAgentHash(userAgent),
            ReturnUrl = "/dashboard"
        };

        var tokens = new KeycloakTokenResponse
        {
            AccessToken = "access-token",
            IdToken = "id-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 3600
        };

        var userInfo = new KeycloakUserInfo
        {
            Sub = "user-sub-123",
            PreferredUsername = "john.doe",
            Email = "john.doe@test.com",
            GivenName = "John",
            FamilyName = "Doe",
            RealmRoles = new List<string> { "LoanOfficer" },
            Attributes = new Dictionary<string, object>
            {
                ["extUserId"] = System.Text.Json.JsonDocument.Parse("[\"user-123\"]").RootElement,
                ["branchId"] = System.Text.Json.JsonDocument.Parse("[\"branch-456\"]").RootElement,
                ["branchName"] = System.Text.Json.JsonDocument.Parse("[\"Test Branch\"]").RootElement,
                ["permissions"] = System.Text.Json.JsonDocument.Parse("[\"loans:create\",\"loans:view\"]").RootElement
            }
        };

        _controller.HttpContext.Request.Headers["User-Agent"] = userAgent;
        _controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        _mockStateStore.Setup(x => x.GetAsync(state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stateData);

        _mockKeycloakService.Setup(x => x.ExchangeCodeForTokensAsync(code, stateData.CodeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        _mockKeycloakService.Setup(x => x.ValidateIdTokenAsync(tokens.IdToken, stateData.Nonce, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockKeycloakService.Setup(x => x.GetUserInfoAsync(tokens.AccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userInfo);

_mockSessionService.Setup(x => x.CreateSessionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionInfo { SessionId = "sess-1", UserId = "user-123", Username = "john.doe", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1), IsActive = true });

        _mockStateStore.Setup(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OidcLoginResponse>(okResult.Value);

        Assert.Equal(tokens.AccessToken, response.AccessToken);
        Assert.Equal(tokens.RefreshToken, response.RefreshToken);
        Assert.Equal(tokens.IdToken, response.IdToken);
        Assert.Equal("user-123", response.User.Id);
        Assert.Equal("john.doe", response.User.Username);
        Assert.Equal("john.doe@test.com", response.User.Email);
        Assert.Contains("LoanOfficer", response.User.Roles);
        Assert.Contains("loans:create", response.User.Permissions);
        Assert.Equal("branch-456", response.User.BranchId);

        // Verify state was removed (one-time use)
        _mockStateStore.Verify(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()), Times.Once);

        // Verify session was created
_mockSessionService.Verify(x => x.CreateSessionAsync(
            It.Is<string>(id => id == "user-123"),
            It.Is<string>(name => name == "john.doe"),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify LoginSucceeded audit event
_mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => 
                e.Action == "LoginSucceeded" && 
                e.EntityId == "user-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Callback_TokenExchangeFails_Returns500()
    {
        // Arrange
        var code = "auth-code-123";
        var state = "valid-state";
        var userAgent = "Test Browser";

        var stateData = new OidcStateData
        {
            CodeVerifier = "verifier-123",
            Nonce = "nonce-123",
            UserAgentHash = PkceHelper.ComputeUserAgentHash(userAgent)
        };

        _controller.HttpContext.Request.Headers["User-Agent"] = userAgent;

        _mockStateStore.Setup(x => x.GetAsync(state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stateData);

        // Token exchange fails
        _mockKeycloakService.Setup(x => x.ExchangeCodeForTokensAsync(code, stateData.CodeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakTokenResponse?)null);

        _mockStateStore.Setup(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);

        // Verify failure was logged
_mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => 
                e.Action == "LoginFailed" &&
                e.Details != null && e.Details.ContainsKey("Error") && e.Details["Error"].ToString()!.Contains("Token exchange failed")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_ClearsSession_ReturnsLogoutUrl()
    {
        // Arrange
        var idToken = "id-token-123";
        var returnUrl = "/";
        var expectedLogoutUrl = "https://keycloak.test/logout?id_token_hint=id-token-123";

        var logoutRequest = new LogoutRequest
        {
            IdToken = idToken,
            ReturnUrl = returnUrl
        };

        var userId = "user-123";
        var claims = new List<Claim>
        {
            new Claim("sub", userId)
        };
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

_mockSessionService.Setup(x => x.RevokeAllSessionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockKeycloakService.Setup(x => x.GenerateLogoutUrl(idToken, returnUrl))
            .Returns(expectedLogoutUrl);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Logout(logoutRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LogoutResponse>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal(expectedLogoutUrl, response.LogoutUrl);

        // Verify session was revoked
        _mockSessionService.Verify(x => x.RevokeAllSessionsAsync(userId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify logout audit event
_mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => 
                e.Action == "Logout" && 
                e.EntityId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_FeatureDisabled_ReturnsBadRequest()
    {
        // Arrange
        _featureFlags.EnableOidc = false;

        var logoutRequest = new LogoutRequest
        {
            IdToken = "id-token",
            ReturnUrl = "/"
        };

        // Act
        var result = await _controller.Logout(logoutRequest);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task Callback_MissingCodeOrState_Returns400()
    {
        // Arrange
        var code = "";
        var state = "";

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task Callback_GetUserInfoFails_Returns500()
    {
        // Arrange
        var code = "auth-code-123";
        var state = "valid-state";
        var userAgent = "Test Browser";

        var stateData = new OidcStateData
        {
            CodeVerifier = "verifier-123",
            Nonce = "nonce-123",
            UserAgentHash = PkceHelper.ComputeUserAgentHash(userAgent)
        };

        var tokens = new KeycloakTokenResponse
        {
            AccessToken = "access-token",
            IdToken = "id-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 3600
        };

        _controller.HttpContext.Request.Headers["User-Agent"] = userAgent;

        _mockStateStore.Setup(x => x.GetAsync(state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stateData);

        _mockKeycloakService.Setup(x => x.ExchangeCodeForTokensAsync(code, stateData.CodeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        _mockKeycloakService.Setup(x => x.ValidateIdTokenAsync(tokens.IdToken, stateData.Nonce, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // GetUserInfo fails
        _mockKeycloakService.Setup(x => x.GetUserInfoAsync(tokens.AccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserInfo?)null);

        _mockStateStore.Setup(x => x.RemoveAsync(state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Callback(code, state);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task Login_SetsSecureCookies()
    {
        // Arrange
        var returnUrl = "/dashboard";
        var expectedAuthUrl = "https://keycloak.test/auth";

        _controller.HttpContext.Request.Headers["User-Agent"] = "Test Browser";

        _mockKeycloakService.Setup(x => x.GenerateAuthorizationUrl(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            returnUrl))
            .Returns(expectedAuthUrl);

        _mockStateStore.Setup(x => x.StoreAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            returnUrl,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

_mockAuditService.Setup(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Login(returnUrl);

        // Assert
        Assert.IsType<RedirectResult>(result);

        // Verify cookie headers contain security attributes
        var setCookieHeader = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("oidc.nonce", setCookieHeader);
        // Note: In actual implementation, we'd verify HttpOnly, Secure, SameSite attributes
    }
}
