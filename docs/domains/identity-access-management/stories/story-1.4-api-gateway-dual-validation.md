# Story 1.4: API Gateway Dual Token Validation

## Story Information

**Epic:** Foundation Setup (Epic 1)  
**Story ID:** 1.4  
**Story Name:** API Gateway Dual Token Validation  
**Priority:** Critical  
**Estimated Effort:** 3 story points (4-6 hours)  
**Dependencies:** Stories 1.2 (Keycloak Config), 1.3 (OIDC Libraries)  
**Blocks:** Story 1.6 (OIDC Flow)

---

## Story Description

As a **Backend Developer**, I want to **extend the API Gateway to validate both custom JWT and Keycloak JWT tokens** so that **the system can support gradual migration from custom authentication to Keycloak without downtime**.

### Business Value

- Enables zero-downtime migration (30-day window)
- Supports dual authentication during transition
- Maintains backward compatibility for existing clients
- Provides flexibility for phased rollout

### User Story

```
Given the existing YARP API Gateway with custom JWT validation
When I configure dual token validation
Then the gateway should accept custom JWT tokens (issuer: IntelliFin.Identity)
And the gateway should accept Keycloak tokens (issuer: https://keycloak.intellifin.local/realms/IntelliFin)
And both token types should be validated correctly
And the gateway should route requests to appropriate services
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** YARP configuration extended to support multiple JWT issuers

- [ ] **AC2:** Custom JWT validation continues to work (no breaking changes)

- [ ] **AC3:** Keycloak JWT validation works when feature flag enabled

- [ ] **AC4:** Token issuer detection based on `iss` claim

- [ ] **AC5:** User context extracted correctly from both token types

### Non-Functional Criteria

- [ ] **AC6:** Token validation latency <50ms (both token types)

- [ ] **AC7:** Feature flag controls Keycloak validation (default: off)

- [ ] **AC8:** Logging differentiates token types in audit trail

- [ ] **AC9:** Health check validates both authentication schemes

- [ ] **AC10:** Existing routes and middleware unchanged

---

## Technical Specification

### Current API Gateway Architecture

**Existing Setup:**
- **Technology:** YARP (Yet Another Reverse Proxy)
- **Location:** `IntelliFin.ApiGateway` project
- **Current Auth:** Custom JWT bearer authentication
- **Issuer:** `IntelliFin.Identity`
- **Signing Key:** HS256 with shared secret

### Target Architecture

**Dual Token Support:**
```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ HTTP + JWT (Custom or Keycloak)
       ▼
┌─────────────────────────────────────┐
│      YARP API Gateway               │
│  ┌──────────────────────────────┐   │
│  │  Authentication Middleware   │   │
│  │  - Detects token issuer      │   │
│  │  - Routes to validator       │   │
│  └──────────┬───────────────────┘   │
│             │                        │
│    ┌────────┴────────┐               │
│    ▼                 ▼               │
│  ┏━━━━━━━━━━━┓   ┏━━━━━━━━━━━━━┓    │
│  ┃  Custom   ┃   ┃   Keycloak  ┃    │
│  ┃  JWT      ┃   ┃   JWT       ┃    │
│  ┃  Validator┃   ┃   Validator ┃    │
│  ┗━━━━━━━━━━━┛   ┗━━━━━━━━━━━━━┛    │
│             │                        │
│    ┌────────┴────────┐               │
│    │  Token Cache    │               │
│    │  (Redis)        │               │
│    └─────────────────┘               │
└──────────────┬──────────────────────┘
               │ Authenticated Request
               ▼
        ┌──────────────┐
        │   Services   │
        └──────────────┘
```

---

## Implementation Steps

### Step 1: Update YARP Configuration

**Location:** `IntelliFin.ApiGateway/appsettings.json`

**Add Keycloak JWT settings:**

```json
{
  "Authentication": {
    "Schemes": {
      "CustomJwt": {
        "Enabled": true,
        "Authority": "https://identity.intellifin.local",
        "Audience": "intellifin-api",
        "Issuer": "IntelliFin.Identity",
        "SigningKey": "{{VAULT:secret/jwt/signing-key}}",
        "ValidateIssuer": true,
        "ValidateAudience": true,
        "ValidateLifetime": true,
        "ClockSkew": "00:05:00"
      },
      "KeycloakJwt": {
        "Enabled": false,
        "Authority": "https://keycloak.intellifin.local/realms/IntelliFin",
        "Audience": "intellifin-identity-service",
        "Issuer": "https://keycloak.intellifin.local/realms/IntelliFin",
        "MetadataAddress": "https://keycloak.intellifin.local/realms/IntelliFin/.well-known/openid-configuration",
        "RequireHttpsMetadata": true,
        "ValidateIssuer": true,
        "ValidateAudience": true,
        "ValidateLifetime": true,
        "ClockSkew": "00:05:00"
      }
    },
    "DefaultScheme": "CustomJwt",
    "ChallengeScheme": "CustomJwt"
  }
}
```

### Step 2: Create Dual Authentication Handler

**Location:** `IntelliFin.ApiGateway/Authentication/DualJwtAuthenticationHandler.cs`

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace IntelliFin.ApiGateway.Authentication;

/// <summary>
/// Authentication handler that supports both custom JWT and Keycloak JWT tokens
/// </summary>
public class DualJwtAuthenticationHandler : AuthenticationHandler<DualJwtAuthenticationOptions>
{
    private readonly ILogger<DualJwtAuthenticationHandler> _logger;
    private readonly IOptionsMonitor<JwtBearerOptions> _customJwtOptions;
    private readonly IOptionsMonitor<JwtBearerOptions> _keycloakJwtOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DualJwtAuthenticationHandler(
        IOptionsMonitor<DualJwtAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<JwtBearerOptions> customJwtOptions,
        IOptionsMonitor<JwtBearerOptions> keycloakJwtOptions,
        IHttpContextAccessor httpContextAccessor)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<DualJwtAuthenticationHandler>();
        _customJwtOptions = customJwtOptions;
        _keycloakJwtOptions = keycloakJwtOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract token from Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var bearerToken = authHeader.ToString();
        if (string.IsNullOrEmpty(bearerToken) || !bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = bearerToken.Substring("Bearer ".Length).Trim();

        // Detect token issuer by decoding (without validation)
        var issuer = GetTokenIssuer(token);

        if (string.IsNullOrEmpty(issuer))
        {
            _logger.LogWarning("Token does not contain issuer claim");
            return AuthenticateResult.Fail("Invalid token: missing issuer");
        }

        // Route to appropriate validator
        AuthenticateResult result;
        if (issuer == Options.CustomJwtIssuer)
        {
            _logger.LogDebug("Validating custom JWT token (issuer: {Issuer})", issuer);
            result = await ValidateCustomJwtAsync(token);
            
            if (result.Succeeded)
            {
                // Mark token type in HttpContext for downstream services
                _httpContextAccessor.HttpContext?.Items.Add("TokenType", "CustomJwt");
            }
        }
        else if (issuer.StartsWith(Options.KeycloakJwtIssuerPrefix))
        {
            if (!Options.EnableKeycloakValidation)
            {
                _logger.LogWarning("Keycloak token received but validation is disabled");
                return AuthenticateResult.Fail("Keycloak authentication is not enabled");
            }

            _logger.LogDebug("Validating Keycloak JWT token (issuer: {Issuer})", issuer);
            result = await ValidateKeycloakJwtAsync(token);
            
            if (result.Succeeded)
            {
                _httpContextAccessor.HttpContext?.Items.Add("TokenType", "KeycloakJwt");
            }
        }
        else
        {
            _logger.LogWarning("Unknown token issuer: {Issuer}", issuer);
            return AuthenticateResult.Fail($"Unknown issuer: {issuer}");
        }

        return result;
    }

    private string? GetTokenIssuer(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Issuer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode JWT token");
            return null;
        }
    }

    private async Task<AuthenticateResult> ValidateCustomJwtAsync(string token)
    {
        var options = _customJwtOptions.Get("CustomJwt");
        
        var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = options.TokenValidationParameters.ValidateIssuer,
            ValidIssuer = options.TokenValidationParameters.ValidIssuer,
            ValidateAudience = options.TokenValidationParameters.ValidateAudience,
            ValidAudience = options.TokenValidationParameters.ValidAudience,
            ValidateLifetime = options.TokenValidationParameters.ValidateLifetime,
            IssuerSigningKey = options.TokenValidationParameters.IssuerSigningKey,
            ClockSkew = options.TokenValidationParameters.ClockSkew
        };

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            var ticket = new AuthenticationTicket(principal, "CustomJwt");
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Custom JWT validation failed");
            return AuthenticateResult.Fail($"Custom JWT validation failed: {ex.Message}");
        }
    }

    private async Task<AuthenticateResult> ValidateKeycloakJwtAsync(string token)
    {
        var options = _keycloakJwtOptions.Get("KeycloakJwt");

        var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = options.TokenValidationParameters.ValidateIssuer,
            ValidIssuer = options.TokenValidationParameters.ValidIssuer,
            ValidateAudience = options.TokenValidationParameters.ValidateAudience,
            ValidAudience = options.TokenValidationParameters.ValidAudience,
            ValidateLifetime = options.TokenValidationParameters.ValidateLifetime,
            IssuerSigningKeys = options.TokenValidationParameters.IssuerSigningKeys,
            ClockSkew = options.TokenValidationParameters.ClockSkew
        };

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            var ticket = new AuthenticationTicket(principal, "KeycloakJwt");
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak JWT validation failed");
            return AuthenticateResult.Fail($"Keycloak JWT validation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Options for dual JWT authentication
/// </summary>
public class DualJwtAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "DualJwt";

    /// <summary>
    /// Custom JWT issuer (e.g., "IntelliFin.Identity")
    /// </summary>
    public string CustomJwtIssuer { get; set; } = "IntelliFin.Identity";

    /// <summary>
    /// Keycloak JWT issuer prefix (e.g., "https://keycloak.intellifin.local/realms/")
    /// </summary>
    public string KeycloakJwtIssuerPrefix { get; set; } = "https://keycloak.intellifin.local/realms/";

    /// <summary>
    /// Enable Keycloak token validation (controlled by feature flag)
    /// </summary>
    public bool EnableKeycloakValidation { get; set; } = false;
}
```

### Step 3: Configure Authentication in Startup

**Location:** `IntelliFin.ApiGateway/Extensions/AuthenticationExtensions.cs`

```csharp
using IntelliFin.ApiGateway.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace IntelliFin.ApiGateway.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddDualJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var customJwtConfig = configuration.GetSection("Authentication:Schemes:CustomJwt");
        var keycloakJwtConfig = configuration.GetSection("Authentication:Schemes:KeycloakJwt");
        var featureFlags = configuration.GetSection("FeatureFlags").Get<FeatureFlagsConfiguration>()
            ?? new FeatureFlagsConfiguration();

        // Configure custom JWT bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = DualJwtAuthenticationOptions.DefaultScheme;
            options.DefaultChallengeScheme = DualJwtAuthenticationOptions.DefaultScheme;
        })
        .AddScheme<DualJwtAuthenticationOptions, DualJwtAuthenticationHandler>(
            DualJwtAuthenticationOptions.DefaultScheme,
            options =>
            {
                options.CustomJwtIssuer = customJwtConfig["Issuer"] ?? "IntelliFin.Identity";
                options.KeycloakJwtIssuerPrefix = keycloakJwtConfig["Authority"] ?? "https://keycloak.intellifin.local/realms/";
                options.EnableKeycloakValidation = featureFlags.EnableDualTokenValidation;
            });

        // Register named JwtBearer options for custom JWT
        services.AddOptions<JwtBearerOptions>("CustomJwt")
            .Configure(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = customJwtConfig.GetValue<bool>("ValidateIssuer"),
                    ValidIssuer = customJwtConfig["Issuer"],
                    ValidateAudience = customJwtConfig.GetValue<bool>("ValidateAudience"),
                    ValidAudience = customJwtConfig["Audience"],
                    ValidateLifetime = customJwtConfig.GetValue<bool>("ValidateLifetime"),
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(customJwtConfig["SigningKey"] ?? throw new InvalidOperationException("Custom JWT signing key not configured"))),
                    ClockSkew = TimeSpan.Parse(customJwtConfig["ClockSkew"] ?? "00:05:00")
                };
            });

        // Register named JwtBearer options for Keycloak JWT
        if (featureFlags.EnableDualTokenValidation)
        {
            services.AddOptions<JwtBearerOptions>("KeycloakJwt")
                .Configure<IHttpClientFactory>((options, httpClientFactory) =>
                {
                    options.Authority = keycloakJwtConfig["Authority"];
                    options.Audience = keycloakJwtConfig["Audience"];
                    options.MetadataAddress = keycloakJwtConfig["MetadataAddress"];
                    options.RequireHttpsMetadata = keycloakJwtConfig.GetValue<bool>("RequireHttpsMetadata");

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = keycloakJwtConfig.GetValue<bool>("ValidateIssuer"),
                        ValidIssuer = keycloakJwtConfig["Issuer"],
                        ValidateAudience = keycloakJwtConfig.GetValue<bool>("ValidateAudience"),
                        ValidAudience = keycloakJwtConfig["Audience"],
                        ValidateLifetime = keycloakJwtConfig.GetValue<bool>("ValidateLifetime"),
                        ClockSkew = TimeSpan.Parse(keycloakJwtConfig["ClockSkew"] ?? "00:05:00")
                    };

                    // Use named HttpClient for OIDC metadata retrieval
                    options.BackchannelHttpHandler = httpClientFactory.CreateClient("Keycloak").Handler;
                });
        }

        return services;
    }
}
```

### Step 4: Update Program.cs

**Location:** `IntelliFin.ApiGateway/Program.cs`

```csharp
// Replace existing authentication configuration
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)...

// With dual authentication
builder.Services.AddDualJwtAuthentication(builder.Configuration);

// Ensure authentication middleware is registered
app.UseAuthentication();
app.UseAuthorization();
```

### Step 5: Add Token Validation Logging

**Location:** `IntelliFin.ApiGateway/Middleware/TokenLoggingMiddleware.cs`

```csharp
namespace IntelliFin.ApiGateway.Middleware;

public class TokenLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenLoggingMiddleware> _logger;

    public TokenLoggingMiddleware(RequestDelegate next, ILogger<TokenLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenType = context.Items["TokenType"]?.ToString() ?? "Unknown";
            var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst("userId")?.Value ?? "Unknown";
            var issuer = context.User.FindFirst("iss")?.Value ?? "Unknown";

            _logger.LogInformation(
                "Authenticated request: User={UserId}, TokenType={TokenType}, Issuer={Issuer}, Path={Path}",
                userId, tokenType, issuer, context.Request.Path);
        }

        await _next(context);
    }
}

// Extension method
public static class TokenLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TokenLoggingMiddleware>();
    }
}
```

**Register in Program.cs:**
```csharp
app.UseTokenLogging(); // After UseAuthentication()
```

---

## Testing Requirements

### Unit Tests

**Location:** `IntelliFin.ApiGateway.Tests/Authentication/`

**Test File:** `DualJwtAuthenticationHandlerTests.cs`

```csharp
[Fact]
public async Task HandleAuthenticateAsync_CustomJwtToken_ReturnsSuccess()
{
    // Arrange
    var customJwtToken = GenerateCustomJwt("user123", "IntelliFin.Identity");
    var context = CreateHttpContext(customJwtToken);
    var handler = CreateHandler(context);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    result.Succeeded.Should().BeTrue();
    context.Items["TokenType"].Should().Be("CustomJwt");
}

[Fact]
public async Task HandleAuthenticateAsync_KeycloakJwtToken_ReturnsSuccess_WhenEnabled()
{
    // Arrange
    var keycloakToken = GenerateKeycloakJwt("user123", "https://keycloak.intellifin.local/realms/IntelliFin");
    var context = CreateHttpContext(keycloakToken);
    var handler = CreateHandler(context, enableKeycloak: true);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    result.Succeeded.Should().BeTrue();
    context.Items["TokenType"].Should().Be("KeycloakJwt");
}

[Fact]
public async Task HandleAuthenticateAsync_KeycloakJwtToken_ReturnsFail_WhenDisabled()
{
    // Arrange
    var keycloakToken = GenerateKeycloakJwt("user123", "https://keycloak.intellifin.local/realms/IntelliFin");
    var context = CreateHttpContext(keycloakToken);
    var handler = CreateHandler(context, enableKeycloak: false);

    // Act
    var result = await handler.AuthenticateAsync();

    // Assert
    result.Succeeded.Should().BeFalse();
    result.Failure?.Message.Should().Contain("not enabled");
}
```

### Integration Tests

**Test both token types:**

```bash
# Test custom JWT
curl -H "Authorization: Bearer $CUSTOM_JWT_TOKEN" \
  https://api-gateway.intellifin.local/api/loans | jq

# Test Keycloak JWT
curl -H "Authorization: Bearer $KEYCLOAK_JWT_TOKEN" \
  https://api-gateway.intellifin.local/api/loans | jq

# Both should return 200 OK with loan data
```

---

## Integration Verification

### Checkpoint 1: Custom JWT Still Works

**Verification:**
```bash
# Get custom JWT token
CUSTOM_TOKEN=$(curl -s -X POST https://identity.intellifin.local/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpass"}' | jq -r '.accessToken')

# Test API Gateway
curl -H "Authorization: Bearer $CUSTOM_TOKEN" \
  https://api-gateway.intellifin.local/api/health

# Expected: 200 OK
```

**Success Criteria:** Existing auth works unchanged

### Checkpoint 2: Keycloak JWT Works When Enabled

**Verification:**
```bash
# Enable feature flag in appsettings.json
# "FeatureFlags": { "EnableDualTokenValidation": true }

# Get Keycloak token
KEYCLOAK_TOKEN=$(curl -s -X POST "https://keycloak.intellifin.local/realms/IntelliFin/protocol/openid-connect/token" \
  -d "client_id=intellifin-identity-service" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "grant_type=password" \
  -d "username=testuser" \
  -d "password=testpass" | jq -r '.access_token')

# Test API Gateway
curl -H "Authorization: Bearer $KEYCLOAK_TOKEN" \
  https://api-gateway.intellifin.local/api/health

# Expected: 200 OK
```

**Success Criteria:** Keycloak auth works

### Checkpoint 3: Token Type Logged

**Verification:**
```bash
# Check logs
kubectl logs -l app=api-gateway -n intellifin --tail=100 | grep "TokenType"

# Expected: Lines showing TokenType=CustomJwt and TokenType=KeycloakJwt
```

**Success Criteria:** Token types differentiated in logs

### Checkpoint 4: Performance Within Tolerance

**Verification:**
```bash
# Load test with custom JWT
ab -n 1000 -c 10 -H "Authorization: Bearer $CUSTOM_TOKEN" \
  https://api-gateway.intellifin.local/api/health

# Load test with Keycloak JWT
ab -n 1000 -c 10 -H "Authorization: Bearer $KEYCLOAK_TOKEN" \
  https://api-gateway.intellifin.local/api/health

# Compare latency (should be <50ms difference)
```

**Success Criteria:** p95 latency <250ms for both

### Checkpoint 5: Feature Flag Controls Keycloak

**Verification:**
```bash
# Disable feature flag
# "FeatureFlags": { "EnableDualTokenValidation": false }

# Restart gateway
kubectl rollout restart deployment/api-gateway -n intellifin

# Test Keycloak token
curl -H "Authorization: Bearer $KEYCLOAK_TOKEN" \
  https://api-gateway.intellifin.local/api/health

# Expected: 401 Unauthorized (Keycloak authentication is not enabled)
```

**Success Criteria:** Keycloak validation disabled

---

## Rollback Plan

### Revert to Single JWT

**Steps:**
1. Set `FeatureFlags:EnableDualTokenValidation` to `false`
2. Restart API Gateway pods
3. Keycloak tokens rejected, custom JWT continues to work

### Full Rollback

```bash
# Revert code changes
git revert <commit-hash>

# Redeploy
kubectl rollout undo deployment/api-gateway -n intellifin
```

---

## Definition of Done

- [ ] Dual authentication handler implemented
- [ ] Custom JWT validation unchanged
- [ ] Keycloak JWT validation works when enabled
- [ ] Feature flag controls Keycloak validation
- [ ] Token type logged in audit trail
- [ ] All unit tests pass
- [ ] Integration tests pass for both token types
- [ ] Performance tests within tolerance
- [ ] All 5 verification checkpoints pass
- [ ] Documentation updated
- [ ] PR merged to `feature/iam-enhancement` branch

---

## Dependencies

**Upstream Dependencies:**
- Story 1.2 (Keycloak Config) - needs realm configured
- Story 1.3 (OIDC Libraries) - needs libraries installed

**Downstream Dependencies:**
- Story 1.6 (OIDC Flow) - needs dual validation enabled

---

## Notes for Developers

### Testing Locally

Generate test tokens:
```bash
# Custom JWT (use existing /api/auth/login)
# Keycloak JWT (use Keycloak direct grant)
```

### Token Issuer Detection

The handler reads the `iss` claim without validation first to route to the correct validator. This is safe because validation happens afterward.

### Clock Skew

Both validators use 5-minute clock skew to handle time synchronization issues between services.

---

**END OF STORY 1.4**
