# Story 1.3: Install OIDC Client Libraries

## Story Information

**Epic:** Foundation Setup (Epic 1)  
**Story ID:** 1.3  
**Story Name:** Install OIDC Client Libraries  
**Priority:** Critical  
**Estimated Effort:** 2 story points (2-4 hours)  
**Dependencies:** Story 1.2 (Keycloak Configuration)  
**Blocks:** Stories 1.5, 1.6 (require OIDC libraries)

---

## Story Description

As a **Backend Developer**, I want to **install and configure OIDC client libraries for .NET** so that **the Identity Service can communicate with Keycloak using industry-standard protocols**.

### Business Value

- Enables secure OIDC authentication flows
- Provides tools for Keycloak Admin API integration
- Reduces custom code by using maintained libraries
- Ensures OAuth2/OIDC compliance

### User Story

```
Given the Identity Service project structure
When I install Keycloak.AuthServices and IdentityModel packages
Then the project should be able to authenticate with Keycloak
And the project should be able to manage users via Admin API
And all OIDC flows should be supported (authorization code, client credentials)
```

---

## Acceptance Criteria

### Functional Criteria

- [ ] **AC1:** NuGet packages installed:
  - `Keycloak.AuthServices.Authentication` (latest stable)
  - `Keycloak.AuthServices.Sdk` (latest stable)
  - `IdentityModel` (latest stable)

- [ ] **AC2:** Keycloak configuration class created with settings:
  - Realm URL
  - Client ID
  - Client secret (loaded from Vault)
  - OIDC endpoints (discovery URL)

- [ ] **AC3:** Dependency injection configured in `ServiceCollectionExtensions.cs`:
  - Keycloak authentication services registered
  - HttpClient with Polly retry policies configured
  - Admin API client registered

- [ ] **AC4:** Feature flags configuration added:
  - `EnableKeycloakIntegration`
  - `EnableDualTokenValidation`
  - `EnableTenancyModel`
  - `EnableSoDEnforcement`

- [ ] **AC5:** Configuration validated at startup (health check)

### Non-Functional Criteria

- [ ] **AC6:** All dependencies compatible with .NET 9.0

- [ ] **AC7:** No breaking changes to existing DI configuration

- [ ] **AC8:** Configuration follows existing patterns (Options pattern)

- [ ] **AC9:** Secrets loaded from Vault, not appsettings.json

- [ ] **AC10:** All new services registered as scoped/transient appropriately

---

## Technical Specification

### NuGet Packages

#### Package 1: Keycloak.AuthServices.Authentication

**Version:** Latest stable (e.g., 2.5.0+)  
**Purpose:** OIDC authentication middleware and token validation  
**Documentation:** https://github.com/NikiforovAll/keycloak-authorization-services-dotnet

**Key Features:**
- OIDC authentication middleware
- JWT bearer token validation
- PKCE support
- Token introspection

#### Package 2: Keycloak.AuthServices.Sdk

**Version:** Latest stable (e.g., 2.5.0+)  
**Purpose:** Keycloak Admin REST API client  
**Documentation:** https://github.com/NikiforovAll/keycloak-authorization-services-dotnet

**Key Features:**
- User management (create, update, delete)
- Client management
- Role assignment
- User attributes management

#### Package 3: IdentityModel

**Version:** Latest stable (e.g., 6.2.0+)  
**Purpose:** OAuth2/OIDC protocol utilities  
**Documentation:** https://github.com/IdentityModel/IdentityModel

**Key Features:**
- Discovery document parsing
- Token request/response handling
- JWKS utilities
- PKCE helper methods

---

## Implementation Steps

### Step 1: Install NuGet Packages

**Command:**

```powershell
cd "IntelliFin.IdentityService"

# Install Keycloak.AuthServices packages
dotnet add package Keycloak.AuthServices.Authentication
dotnet add package Keycloak.AuthServices.Sdk

# Install IdentityModel
dotnet add package IdentityModel

# Verify installations
dotnet list package
```

**Expected Output:**
```
Project 'IntelliFin.IdentityService' has the following package references
   [net9.0]:
   Top-level Package                                Requested   Resolved
   > Keycloak.AuthServices.Authentication          *           2.5.3
   > Keycloak.AuthServices.Sdk                     *           2.5.3
   > IdentityModel                                 *           6.2.0
```

### Step 2: Create Configuration Classes

**Location:** `IntelliFin.IdentityService/Configuration/KeycloakConfiguration.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace IntelliFin.IdentityService.Configuration;

/// <summary>
/// Configuration settings for Keycloak integration
/// </summary>
public class KeycloakConfiguration
{
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Keycloak server base URL (e.g., https://keycloak.intellifin.local)
    /// </summary>
    [Required]
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Realm name (e.g., IntelliFin)
    /// </summary>
    [Required]
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// OIDC client ID for Identity Service
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret (loaded from Vault at runtime)
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Admin API client ID for user provisioning
    /// </summary>
    [Required]
    public string AdminClientId { get; set; } = string.Empty;

    /// <summary>
    /// Admin client secret (loaded from Vault at runtime)
    /// </summary>
    [Required]
    public string AdminClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// OIDC discovery endpoint (computed from ServerUrl and Realm)
    /// </summary>
    public string AuthorityUrl => $"{ServerUrl}/realms/{Realm}";

    /// <summary>
    /// Token endpoint URL
    /// </summary>
    public string TokenEndpoint => $"{AuthorityUrl}/protocol/openid-connect/token";

    /// <summary>
    /// Authorization endpoint URL
    /// </summary>
    public string AuthorizationEndpoint => $"{AuthorityUrl}/protocol/openid-connect/auth";

    /// <summary>
    /// UserInfo endpoint URL
    /// </summary>
    public string UserInfoEndpoint => $"{AuthorityUrl}/protocol/openid-connect/userinfo";

    /// <summary>
    /// Logout endpoint URL
    /// </summary>
    public string LogoutEndpoint => $"{AuthorityUrl}/protocol/openid-connect/logout";

    /// <summary>
    /// Admin API base URL
    /// </summary>
    public string AdminApiUrl => $"{ServerUrl}/admin/realms/{Realm}";

    /// <summary>
    /// Whether to require HTTPS (set to false for local dev)
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Whether to validate issuer (set to false for testing)
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Whether to validate audience
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Token cache duration in minutes (default 55 minutes, token expires in 60)
    /// </summary>
    public int TokenCacheDurationMinutes { get; set; } = 55;
}
```

**Location:** `IntelliFin.IdentityService/Configuration/FeatureFlagsConfiguration.cs`

```csharp
namespace IntelliFin.IdentityService.Configuration;

/// <summary>
/// Feature flags for gradual IAM enhancement rollout
/// </summary>
public class FeatureFlagsConfiguration
{
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// Enable Keycloak OIDC authentication flow (default: false)
    /// </summary>
    public bool EnableKeycloakIntegration { get; set; } = false;

    /// <summary>
    /// Enable dual token validation in API Gateway (custom JWT + Keycloak JWT)
    /// </summary>
    public bool EnableDualTokenValidation { get; set; } = false;

    /// <summary>
    /// Enable tenant model and multi-tenancy features
    /// </summary>
    public bool EnableTenancyModel { get; set; } = false;

    /// <summary>
    /// Enable Separation of Duties enforcement
    /// </summary>
    public bool EnableSoDEnforcement { get; set; } = false;

    /// <summary>
    /// Enable user provisioning to Keycloak
    /// </summary>
    public bool EnableUserProvisioning { get; set; } = false;

    /// <summary>
    /// Enable local audit event storage (supplement to Admin Service)
    /// </summary>
    public bool EnableLocalAuditStorage { get; set; } = false;
}
```

### Step 3: Update appsettings.json

**Location:** `IntelliFin.IdentityService/appsettings.json`

```json
{
  "Keycloak": {
    "ServerUrl": "https://keycloak.intellifin.local",
    "Realm": "IntelliFin",
    "ClientId": "intellifin-identity-service",
    "ClientSecret": "{{VAULT:secret/keycloak/clients/intellifin-identity-service/secret}}",
    "AdminClientId": "intellifin-identity-service-admin",
    "AdminClientSecret": "{{VAULT:secret/keycloak/clients/intellifin-identity-service-admin/secret}}",
    "RequireHttpsMetadata": true,
    "ValidateIssuer": true,
    "ValidateAudience": true,
    "TokenCacheDurationMinutes": 55
  },
  "FeatureFlags": {
    "EnableKeycloakIntegration": false,
    "EnableDualTokenValidation": false,
    "EnableTenancyModel": false,
    "EnableSoDEnforcement": false,
    "EnableUserProvisioning": false,
    "EnableLocalAuditStorage": false
  }
}
```

**Environment-specific overrides:**

`appsettings.Development.json`:
```json
{
  "Keycloak": {
    "ServerUrl": "http://localhost:8080",
    "RequireHttpsMetadata": false
  }
}
```

### Step 4: Configure Dependency Injection

**Location:** `IntelliFin.IdentityService/Extensions/ServiceCollectionExtensions.cs`

**Add method:**

```csharp
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Sdk;
using Keycloak.AuthServices.Sdk.Admin;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace IntelliFin.IdentityService.Extensions;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure Keycloak integration services
    /// </summary>
    public static IServiceCollection AddKeycloakIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<KeycloakConfiguration>(configuration.GetSection(KeycloakConfiguration.SectionName));
        services.Configure<FeatureFlagsConfiguration>(configuration.GetSection(FeatureFlagsConfiguration.SectionName));

        // Validate configuration at startup
        services.AddOptions<KeycloakConfiguration>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Load secrets from Vault
        services.AddSingleton<IPostConfigureOptions<KeycloakConfiguration>, KeycloakVaultConfigurationProvider>();

        // Get feature flags to conditionally register services
        var featureFlags = configuration.GetSection(FeatureFlagsConfiguration.SectionName).Get<FeatureFlagsConfiguration>()
            ?? new FeatureFlagsConfiguration();

        if (featureFlags.EnableKeycloakIntegration || featureFlags.EnableDualTokenValidation)
        {
            // Register Keycloak authentication services
            services.AddKeycloakAuthentication(configuration);

            // Register HttpClient with retry policies for Keycloak API calls
            services.AddHttpClient("Keycloak")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            // Register Keycloak Admin API client
            services.AddKeycloakAdminHttpClient(configuration);

            // Register custom Keycloak services (will be created in later stories)
            services.AddScoped<IKeycloakService, KeycloakService>();
            services.AddScoped<IKeycloakUserProvisioningService, KeycloakProvisioningService>();
        }

        return services;
    }

    /// <summary>
    /// Configure Keycloak authentication middleware
    /// </summary>
    private static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloakConfig = configuration.GetSection(KeycloakConfiguration.SectionName).Get<KeycloakConfiguration>()
            ?? throw new InvalidOperationException("Keycloak configuration is missing");

        services.AddKeycloakWebApiAuthentication(configuration, options =>
        {
            options.Realm = keycloakConfig.Realm;
            options.AuthServerUrl = keycloakConfig.ServerUrl;
            options.Resource = keycloakConfig.ClientId;
            options.VerifyTokenAudience = keycloakConfig.ValidateAudience;
            options.RequireHttpsMetadata = keycloakConfig.RequireHttpsMetadata;
        });

        return services;
    }

    /// <summary>
    /// Configure Keycloak Admin API HTTP client
    /// </summary>
    private static IServiceCollection AddKeycloakAdminHttpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloakConfig = configuration.GetSection(KeycloakConfiguration.SectionName).Get<KeycloakConfiguration>()
            ?? throw new InvalidOperationException("Keycloak configuration is missing");

        services.AddKeycloakAdminHttpClient(options =>
        {
            options.AuthServerUrl = keycloakConfig.ServerUrl;
            options.Realm = keycloakConfig.Realm;
        });

        return services;
    }

    /// <summary>
    /// Polly retry policy for transient HTTP errors
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning(
                        "Keycloak API call failed. Retry attempt {RetryAttempt} after {Delay}ms. Error: {Error}",
                        retryAttempt,
                        timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                });
    }

    /// <summary>
    /// Polly circuit breaker policy
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (outcome, duration) =>
                {
                    // Log circuit breaker opened
                },
                onReset: () =>
                {
                    // Log circuit breaker reset
                });
    }
}

/// <summary>
/// Load Keycloak secrets from HashiCorp Vault
/// </summary>
public class KeycloakVaultConfigurationProvider : IPostConfigureOptions<KeycloakConfiguration>
{
    private readonly IVaultService _vaultService;
    private readonly ILogger<KeycloakVaultConfigurationProvider> _logger;

    public KeycloakVaultConfigurationProvider(
        IVaultService vaultService,
        ILogger<KeycloakVaultConfigurationProvider> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    public void PostConfigure(string? name, KeycloakConfiguration options)
    {
        // Replace vault placeholders with actual secrets
        if (options.ClientSecret.StartsWith("{{VAULT:") && options.ClientSecret.EndsWith("}}"))
        {
            var vaultPath = options.ClientSecret[8..^2]; // Extract path from {{VAULT:path}}
            options.ClientSecret = _vaultService.GetSecretAsync(vaultPath).GetAwaiter().GetResult();
            _logger.LogDebug("Loaded Keycloak client secret from Vault: {Path}", vaultPath);
        }

        if (options.AdminClientSecret.StartsWith("{{VAULT:") && options.AdminClientSecret.EndsWith("}}"))
        {
            var vaultPath = options.AdminClientSecret[8..^2];
            options.AdminClientSecret = _vaultService.GetSecretAsync(vaultPath).GetAwaiter().GetResult();
            _logger.LogDebug("Loaded Keycloak admin client secret from Vault: {Path}", vaultPath);
        }
    }
}
```

### Step 5: Register Services in Program.cs

**Location:** `IntelliFin.IdentityService/Program.cs`

**Add after existing service registrations:**

```csharp
// Add Keycloak integration (controlled by feature flags)
builder.Services.AddKeycloakIntegration(builder.Configuration);
```

### Step 6: Add Health Check

**Location:** `IntelliFin.IdentityService/HealthChecks/KeycloakHealthCheck.cs`

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace IntelliFin.IdentityService.HealthChecks;

/// <summary>
/// Health check for Keycloak connectivity
/// </summary>
public class KeycloakHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakConfiguration _config;
    private readonly FeatureFlagsConfiguration _featureFlags;
    private readonly ILogger<KeycloakHealthCheck> _logger;

    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakConfiguration> config,
        IOptions<FeatureFlagsConfiguration> featureFlags,
        ILogger<KeycloakHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Skip health check if Keycloak integration is disabled
        if (!_featureFlags.EnableKeycloakIntegration && !_featureFlags.EnableDualTokenValidation)
        {
            return HealthCheckResult.Healthy("Keycloak integration disabled (feature flag off)");
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("Keycloak");
            var discoveryUrl = $"{_config.AuthorityUrl}/.well-known/openid-configuration";

            var response = await httpClient.GetAsync(discoveryUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = new Dictionary<string, object>
                {
                    { "realm", _config.Realm },
                    { "discoveryUrl", discoveryUrl },
                    { "status", "connected" }
                };

                return HealthCheckResult.Healthy("Keycloak is reachable", data);
            }

            return HealthCheckResult.Unhealthy(
                $"Keycloak returned status code: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Keycloak health check failed");
            return HealthCheckResult.Unhealthy(
                "Cannot reach Keycloak server",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak health check failed with unexpected error");
            return HealthCheckResult.Unhealthy(
                "Keycloak health check failed",
                ex);
        }
    }
}
```

**Register health check in Program.cs:**

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<KeycloakHealthCheck>("keycloak", tags: new[] { "keycloak", "identity" });
```

---

## Testing Requirements

### Unit Tests

**Location:** `IntelliFin.IdentityService.Tests/Configuration/`

**Test File:** `KeycloakConfigurationTests.cs`

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntelliFin.IdentityService.Tests.Configuration;

public class KeycloakConfigurationTests
{
    [Fact]
    public void KeycloakConfiguration_BindsFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Keycloak:ServerUrl"] = "https://keycloak.test.local",
                ["Keycloak:Realm"] = "TestRealm",
                ["Keycloak:ClientId"] = "test-client",
                ["Keycloak:ClientSecret"] = "test-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<KeycloakConfiguration>(config.GetSection("Keycloak"));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var keycloakConfig = serviceProvider.GetRequiredService<IOptions<KeycloakConfiguration>>().Value;

        // Assert
        keycloakConfig.ServerUrl.Should().Be("https://keycloak.test.local");
        keycloakConfig.Realm.Should().Be("TestRealm");
        keycloakConfig.ClientId.Should().Be("test-client");
    }

    [Fact]
    public void AuthorityUrl_ComputedCorrectly()
    {
        // Arrange
        var config = new KeycloakConfiguration
        {
            ServerUrl = "https://keycloak.test.local",
            Realm = "TestRealm"
        };

        // Act
        var authorityUrl = config.AuthorityUrl;

        // Assert
        authorityUrl.Should().Be("https://keycloak.test.local/realms/TestRealm");
    }

    [Fact]
    public void FeatureFlagsConfiguration_DefaultsToFalse()
    {
        // Arrange & Act
        var featureFlags = new FeatureFlagsConfiguration();

        // Assert
        featureFlags.EnableKeycloakIntegration.Should().BeFalse();
        featureFlags.EnableDualTokenValidation.Should().BeFalse();
        featureFlags.EnableTenancyModel.Should().BeFalse();
        featureFlags.EnableSoDEnforcement.Should().BeFalse();
    }
}
```

### Integration Tests

**Test Keycloak Connectivity:**

```csharp
[Fact]
public async Task KeycloakHealthCheck_ReturnsHealthy_WhenKeycloakReachable()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddHttpClient();
    services.Configure<KeycloakConfiguration>(options =>
    {
        options.ServerUrl = "https://keycloak.intellifin.local";
        options.Realm = "IntelliFin";
    });
    services.Configure<FeatureFlagsConfiguration>(options =>
    {
        options.EnableKeycloakIntegration = true;
    });
    services.AddLogging();

    var serviceProvider = services.BuildServiceProvider();
    var healthCheck = new KeycloakHealthCheck(
        serviceProvider.GetRequiredService<IHttpClientFactory>(),
        serviceProvider.GetRequiredService<IOptions<KeycloakConfiguration>>(),
        serviceProvider.GetRequiredService<IOptions<FeatureFlagsConfiguration>>(),
        serviceProvider.GetRequiredService<ILogger<KeycloakHealthCheck>>()
    );

    // Act
    var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
}
```

---

## Integration Verification

### Checkpoint 1: NuGet Packages Installed

**Verification:**
```powershell
cd IntelliFin.IdentityService
dotnet list package | Select-String "Keycloak|IdentityModel"
```

**Success Criteria:** All 3 packages listed

### Checkpoint 2: Configuration Classes Created

**Verification:**
```powershell
Test-Path "IntelliFin.IdentityService/Configuration/KeycloakConfiguration.cs"
Test-Path "IntelliFin.IdentityService/Configuration/FeatureFlagsConfiguration.cs"
```

**Success Criteria:** Both files exist

### Checkpoint 3: DI Registration Compiles

**Verification:**
```powershell
dotnet build IntelliFin.IdentityService
```

**Success Criteria:** Build succeeds with no errors

### Checkpoint 4: Health Check Responds

**Verification:**
```powershell
# Start application
dotnet run --project IntelliFin.IdentityService

# Check health endpoint
curl http://localhost:5001/health | jq '.entries.keycloak'
```

**Success Criteria:** Returns health status

### Checkpoint 5: Existing Tests Pass

**Verification:**
```powershell
dotnet test IntelliFin.IdentityService.Tests
```

**Success Criteria:** All tests pass

---

## Rollback Plan

### Uninstall Packages

```powershell
cd IntelliFin.IdentityService
dotnet remove package Keycloak.AuthServices.Authentication
dotnet remove package Keycloak.AuthServices.Sdk
dotnet remove package IdentityModel
```

### Remove Configuration

```powershell
Remove-Item "IntelliFin.IdentityService/Configuration/KeycloakConfiguration.cs"
Remove-Item "IntelliFin.IdentityService/Configuration/FeatureFlagsConfiguration.cs"
```

### Revert DI Changes

Remove `AddKeycloakIntegration` call from `Program.cs`

---

## Definition of Done

- [ ] All 3 NuGet packages installed
- [ ] KeycloakConfiguration class created
- [ ] FeatureFlagsConfiguration class created
- [ ] appsettings.json updated with Keycloak settings
- [ ] Dependency injection configured in ServiceCollectionExtensions
- [ ] Vault integration for client secrets implemented
- [ ] Health check created and registered
- [ ] All unit tests pass
- [ ] Build succeeds with no warnings
- [ ] Configuration validated at startup
- [ ] PR merged to `feature/iam-enhancement` branch

---

## Dependencies

**Upstream Dependencies:**
- Story 1.2 (Keycloak Configuration) - needs realm and clients configured

**Downstream Dependencies:**
- Story 1.5 (User Provisioning) - needs Admin API client
- Story 1.6 (OIDC Flow) - needs OIDC services

---

## Notes for Developers

### Package Version Compatibility

Always use matching versions of `Keycloak.AuthServices.*` packages:
```xml
<PackageReference Include="Keycloak.AuthServices.Authentication" Version="2.5.3" />
<PackageReference Include="Keycloak.AuthServices.Sdk" Version="2.5.3" />
```

### Local Development Without Vault

For local development without Vault, use User Secrets:
```powershell
dotnet user-secrets set "Keycloak:ClientSecret" "your-client-secret"
dotnet user-secrets set "Keycloak:AdminClientSecret" "your-admin-secret"
```

### Feature Flag Testing

Test with feature flags disabled:
```json
{
  "FeatureFlags": {
    "EnableKeycloakIntegration": false
  }
}
```

Services should not be registered, health check should return "disabled" status.

---

**END OF STORY 1.3**
