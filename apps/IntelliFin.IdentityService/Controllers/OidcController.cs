using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IntelliFin.IdentityService.Controllers;

/// <summary>
/// Controller for OIDC authentication flows
/// </summary>
[ApiController]
[Route("api/auth/oidc")]
public class OidcController : ControllerBase
{
    private readonly IKeycloakService _keycloakService;
    private readonly IOidcStateStore _stateStore;
    private readonly ISessionService _sessionService;
    private readonly IAuditService _auditService;
    private readonly FeatureFlags _featureFlags;
    private readonly ILogger<OidcController> _logger;

    public OidcController(
        IKeycloakService keycloakService,
        IOidcStateStore stateStore,
        ISessionService sessionService,
        IAuditService auditService,
        IOptions<FeatureFlags> featureFlags,
        ILogger<OidcController> logger)
    {
        _keycloakService = keycloakService;
        _stateStore = stateStore;
        _sessionService = sessionService;
        _auditService = auditService;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initiate OIDC login flow with PKCE
    /// GET /api/auth/oidc/login?returnUrl=/dashboard
    /// </summary>
    [HttpGet("login")]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl)
    {
        if (!_featureFlags.EnableOidc)
        {
            return BadRequest(new { error = "OIDC authentication is not enabled" });
        }

        var correlationId = Guid.NewGuid();

        try
        {
            // Generate PKCE parameters
            var state = PkceHelper.GenerateState();
            var codeVerifier = PkceHelper.GenerateCodeVerifier();
            var codeChallenge = PkceHelper.ComputeCodeChallenge(codeVerifier);
            var nonce = PkceHelper.GenerateNonce();

            // Get user agent hash for binding
            var userAgent = Request.Headers["User-Agent"].ToString();
            var userAgentHash = PkceHelper.ComputeUserAgentHash(userAgent);

            // Store state and PKCE data in Redis
            await _stateStore.StoreAsync(state, codeVerifier, nonce, returnUrl, userAgentHash);

            // Set nonce cookie (HttpOnly, Secure, SameSite=Strict)
            Response.Cookies.Append("oidc.nonce", nonce, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromMinutes(10)
            });

            // Log audit event
            await _auditService.LogEventAsync(new AuditEvent
            {
                Action = "LoginStarted",
                Entity = "OidcFlow",
                EntityId = state,
                Details = JsonSerializer.Serialize(new
                {
                    CorrelationId = correlationId,
                    ReturnUrl = returnUrl,
                    Method = "OIDC"
                }),
                ActorId = "Anonymous"
            });

            // Generate authorization URL
            var authUrl = _keycloakService.GenerateAuthorizationUrl(state, codeChallenge, nonce, returnUrl);

            _logger.LogInformation(
                "Initiating OIDC login. State: {State}, CorrelationId: {CorrelationId}",
                state,
                correlationId);

            // Redirect to Keycloak
            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating OIDC login. CorrelationId: {CorrelationId}", correlationId);

            await _auditService.LogEventAsync(new AuditEvent
            {
                Action = "LoginFailed",
                Entity = "OidcFlow",
                EntityId = correlationId.ToString(),
                Details = JsonSerializer.Serialize(new
                {
                    CorrelationId = correlationId,
                    Error = ex.Message,
                    Method = "OIDC"
                }),
                ActorId = "Anonymous"
            });

            return StatusCode(500, new { error = "Failed to initiate login" });
        }
    }

    /// <summary>
    /// Handle OIDC callback from Keycloak
    /// GET /api/auth/oidc/callback?code=xxx&state=yyy
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (!_featureFlags.EnableOidc)
        {
            return BadRequest(new { error = "OIDC authentication is not enabled" });
        }

        var correlationId = Guid.NewGuid();

        try
        {
            // Validate state parameter
            if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Missing code or state parameter");
                return BadRequest(new { error = "Invalid callback parameters" });
            }

            // Retrieve state data from Redis
            var stateData = await _stateStore.GetAsync(state);
            if (stateData == null)
            {
                _logger.LogWarning("State {State} not found or expired", state);
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    Action = "LoginFailed",
                    Entity = "OidcFlow",
                    EntityId = state,
                    Details = JsonSerializer.Serialize(new
                    {
                        CorrelationId = correlationId,
                        Error = "Invalid or expired state",
                        Method = "OIDC"
                    }),
                    ActorId = "Anonymous"
                });

                return BadRequest(new { error = "Invalid or expired state" });
            }

            // Validate user agent binding
            var userAgent = Request.Headers["User-Agent"].ToString();
            var currentUaHash = PkceHelper.ComputeUserAgentHash(userAgent);
            
            if (stateData.UserAgentHash != currentUaHash)
            {
                _logger.LogWarning("User agent mismatch for state {State}", state);
                await _stateStore.RemoveAsync(state);

                await _auditService.LogEventAsync(new AuditEvent
                {
                    Action = "LoginFailed",
                    Entity = "OidcFlow",
                    EntityId = state,
                    Details = JsonSerializer.Serialize(new
                    {
                        CorrelationId = correlationId,
                        Error = "User agent mismatch (potential CSRF)",
                        Method = "OIDC"
                    }),
                    ActorId = "Anonymous"
                });

                return Unauthorized(new { error = "Authentication failed" });
            }

            // Exchange code for tokens
            var tokens = await _keycloakService.ExchangeCodeForTokensAsync(code, stateData.CodeVerifier);
            if (tokens == null)
            {
                _logger.LogError("Failed to exchange code for tokens");
                await _stateStore.RemoveAsync(state);

                await _auditService.LogEventAsync(new AuditEvent
                {
                    Action = "LoginFailed",
                    Entity = "OidcFlow",
                    EntityId = state,
                    Details = JsonSerializer.Serialize(new
                    {
                        CorrelationId = correlationId,
                        Error = "Token exchange failed",
                        Method = "OIDC"
                    }),
                    ActorId = "Anonymous"
                });

                return StatusCode(500, new { error = "Failed to obtain tokens" });
            }

            // Validate ID token
            var idTokenValid = await _keycloakService.ValidateIdTokenAsync(tokens.IdToken, stateData.Nonce);
            if (!idTokenValid)
            {
                _logger.LogError("ID token validation failed");
                await _stateStore.RemoveAsync(state);

                await _auditService.LogEventAsync(new AuditEvent
                {
                    Action = "LoginFailed",
                    Entity = "OidcFlow",
                    EntityId = state,
                    Details = JsonSerializer.Serialize(new
                    {
                        CorrelationId = correlationId,
                        Error = "ID token validation failed",
                        Method = "OIDC"
                    }),
                    ActorId = "Anonymous"
                });

                return Unauthorized(new { error = "Invalid ID token" });
            }

            // Get user info from Keycloak
            var userInfo = await _keycloakService.GetUserInfoAsync(tokens.AccessToken);
            if (userInfo == null)
            {
                _logger.LogError("Failed to get user info");
                await _stateStore.RemoveAsync(state);
                return StatusCode(500, new { error = "Failed to get user information" });
            }

            // Map to response DTO
            var response = await MapToResponseAsync(tokens, userInfo);

            // Create local session
            var sessionId = await CreateSessionAsync(response.User, tokens);

            // Remove state from Redis (one-time use)
            await _stateStore.RemoveAsync(state);

            // Remove nonce cookie
            Response.Cookies.Delete("oidc.nonce");

            // Log successful login
            await _auditService.LogEventAsync(new AuditEvent
            {
                Action = "LoginSucceeded",
                Entity = "User",
                EntityId = response.User.Id,
                Details = JsonSerializer.Serialize(new
                {
                    CorrelationId = correlationId,
                    Username = response.User.Username,
                    Method = "OIDC",
                    SessionId = sessionId
                }),
                ActorId = response.User.Id
            });

            _logger.LogInformation(
                "OIDC login successful for user {UserId}. CorrelationId: {CorrelationId}",
                response.User.Id,
                correlationId);

            // Return response
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OIDC callback. CorrelationId: {CorrelationId}", correlationId);

            await _auditService.LogEventAsync(new AuditEvent
            {
                Action = "LoginFailed",
                Entity = "OidcFlow",
                EntityId = state ?? "unknown",
                Details = JsonSerializer.Serialize(new
                {
                    CorrelationId = correlationId,
                    Error = ex.Message,
                    Method = "OIDC"
                }),
                ActorId = "Anonymous"
            });

            return StatusCode(500, new { error = "Authentication failed" });
        }
    }

    /// <summary>
    /// Logout from OIDC session
    /// POST /api/auth/oidc/logout
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        if (!_featureFlags.EnableOidc)
        {
            return BadRequest(new { error = "OIDC authentication is not enabled" });
        }

        try
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;

            // Clear local session
            if (!string.IsNullOrEmpty(userId))
            {
                await _sessionService.RevokeAllSessionsAsync(userId);

                await _auditService.LogEventAsync(new AuditEvent
                {
                    Action = "Logout",
                    Entity = "User",
                    EntityId = userId,
                    Details = JsonSerializer.Serialize(new
                    {
                        Method = "OIDC"
                    }),
                    ActorId = userId
                });
            }

            // Generate Keycloak logout URL
            var logoutUrl = _keycloakService.GenerateLogoutUrl(request.IdToken, request.ReturnUrl);

            _logger.LogInformation("OIDC logout for user {UserId}", userId ?? "unknown");

            return Ok(new LogoutResponse
            {
                LogoutUrl = logoutUrl,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OIDC logout");
            return StatusCode(500, new { error = "Logout failed" });
        }
    }

    private async Task<OidcLoginResponse> MapToResponseAsync(KeycloakTokenResponse tokens, KeycloakUserInfo userInfo)
    {
        // Extract claims from attributes
        var attributes = userInfo.Attributes ?? new Dictionary<string, object>();
        
        string? GetAttributeValue(string key)
        {
            if (attributes.TryGetValue(key, out var value))
            {
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    return element.EnumerateArray().FirstOrDefault().GetString();
                }
                return value?.ToString();
            }
            return null;
        }

        List<string> GetAttributeArray(string key)
        {
            if (attributes.TryGetValue(key, out var value))
            {
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    return element.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();
                }
            }
            return new List<string>();
        }

        var response = new OidcLoginResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            IdToken = tokens.IdToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = tokens.TokenType,
            User = new UserInfo
            {
                Id = GetAttributeValue("extUserId") ?? userInfo.Sub,
                Username = userInfo.PreferredUsername,
                Email = userInfo.Email,
                FirstName = userInfo.GivenName,
                LastName = userInfo.FamilyName,
                Roles = userInfo.RealmRoles ?? new List<string>(),
                Permissions = GetAttributeArray("permissions"),
                BranchId = GetAttributeValue("branchId"),
                BranchName = GetAttributeValue("branchName"),
                BranchRegion = GetAttributeValue("branchRegion"),
                TenantId = GetAttributeArray("tenantId").FirstOrDefault(),
                TenantName = GetAttributeArray("tenantName").FirstOrDefault()
            }
        };

        return response;
    }

    private async Task<string> CreateSessionAsync(UserInfo user, KeycloakTokenResponse tokens)
    {
        // Create session info
        var sessionInfo = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = Request.Headers["User-Agent"].ToString()
        };

        // Store session (implementation depends on ISessionService)
        await _sessionService.CreateSessionAsync(sessionInfo);

        // Set session cookie
        Response.Cookies.Append("session_id", sessionInfo.SessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromSeconds(tokens.ExpiresIn)
        });

        return sessionInfo.SessionId;
    }
}
