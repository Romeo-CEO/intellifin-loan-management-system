using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.HealthChecks;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Extensions;

/// <summary>
/// Extension methods for configuring Keycloak OIDC authentication
/// </summary>
public static class KeycloakAuthenticationExtensions
{
    /// <summary>
    /// Adds Keycloak OIDC authentication services to the application
    /// </summary>
    public static AuthenticationBuilder AddKeycloakAuthentication(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var services = builder.Services;
        // Bind Keycloak configuration
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        
        var keycloakOptions = configuration
            .GetSection(KeycloakOptions.SectionName)
            .Get<KeycloakOptions>() ?? new KeycloakOptions();

        // Register core services (always register for detection/mapping even if disabled)
        services.AddScoped<ITokenIssuerDetector, TokenIssuerDetector>();
        services.AddScoped<IKeycloakRoleMapper, KeycloakRoleMapper>();
        
        // Register Keycloak admin client for user provisioning
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>(client =>
        {
            client.BaseAddress = new Uri(keycloakOptions.Authority);
            client.Timeout = TimeSpan.FromSeconds(keycloakOptions.Connection.TimeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (!keycloakOptions.RequireHttpsMetadata)
            {
                handler.ServerCertificateCustomValidationCallback = 
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });
        
// Register user provisioning service
        services.AddScoped<IUserProvisioningService, KeycloakProvisioningService>();

        // Skip further configuration if Keycloak is not enabled
        if (!keycloakOptions.Enabled)
        {
            return builder;
        }

        // Register Keycloak HTTP client with retry policy
        services.AddHttpClient<IKeycloakHttpClient, KeycloakHttpClient>(client =>
        {
            client.BaseAddress = new Uri(keycloakOptions.Authority);
            client.Timeout = TimeSpan.FromSeconds(keycloakOptions.Connection.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy(keycloakOptions.Connection.MaxRetries, 
                                         keycloakOptions.Connection.RetryDelayMs))
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            
            // Skip SSL validation in development if configured
            if (!keycloakOptions.RequireHttpsMetadata)
            {
                handler.ServerCertificateCustomValidationCallback = 
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            
            return handler;
        });

        // Add Keycloak-based JWT Bearer authentication
        builder.AddJwtBearer("Keycloak", options =>
            {
                var realmUrl = keycloakOptions.GetRealmUrl();
                
                options.Authority = realmUrl;
                options.Audience = keycloakOptions.ClientId;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                options.MetadataAddress = keycloakOptions.GetDiscoveryEndpoint();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = keycloakOptions.TokenValidation.ValidateIssuer,
                    ValidIssuer = realmUrl,
                    ValidateAudience = keycloakOptions.TokenValidation.ValidateAudience,
                    ValidAudience = keycloakOptions.ClientId,
                    ValidateLifetime = keycloakOptions.TokenValidation.ValidateLifetime,
                    ValidateIssuerSigningKey = keycloakOptions.TokenValidation.ValidateIssuerSigningKey,
                    RequireExpirationTime = keycloakOptions.TokenValidation.RequireExpirationTime,
                    ClockSkew = TimeSpan.FromMinutes(keycloakOptions.TokenValidation.ClockSkewMinutes),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                // Configure metadata refresh
                options.RefreshInterval = TimeSpan.FromMinutes(
                    keycloakOptions.TokenValidation.MetadataCacheDurationMinutes);

                // Event handlers
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        
                        var userName = context.Principal?.Identity?.Name;
                        logger.LogDebug("Keycloak token validated for user: {UserName}", userName);
                        
                        // Map Keycloak roles to claims
                        if (context.Principal != null)
                        {
                            var roleMapper = context.HttpContext.RequestServices
                                .GetRequiredService<IKeycloakRoleMapper>();
                            roleMapper.MapRolesToClaims(context.Principal);
                        }
                        
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        
                        logger.LogWarning("Keycloak authentication failed: {Error}", 
                            context.Exception.Message);
                        
                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        
                        logger.LogDebug("Keycloak token received from request");
                        
                        return Task.CompletedTask;
                    }
                };
            });

        return builder;
    }

    /// <summary>
    /// Adds Keycloak health checks
    /// </summary>
    public static IHealthChecksBuilder AddKeycloakHealthChecks(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        var keycloakOptions = configuration
            .GetSection(KeycloakOptions.SectionName)
            .Get<KeycloakOptions>();

        if (keycloakOptions?.Enabled == true)
        {
            builder.AddCheck<KeycloakHealthCheck>(
                "keycloak",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "keycloak", "oidc", "ready" });

            builder.AddCheck<KeycloakDiscoveryHealthCheck>(
                "keycloak_discovery",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "keycloak", "oidc", "discovery" });
        }

        return builder;
    }

    /// <summary>
    /// Configures dual-mode JWT authentication (custom JWT + Keycloak)
    /// </summary>
    public static AuthenticationBuilder AddDualModeJwtAuthentication(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        var keycloakOptions = configuration
            .GetSection(KeycloakOptions.SectionName)
            .Get<KeycloakOptions>();

        if (keycloakOptions?.Enabled == true && keycloakOptions.DualModeEnabled)
        {
            // Configure policy scheme to intelligently route to the correct authentication scheme
            builder.AddPolicyScheme("DualMode", "Dual Mode Authentication", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authHeader = context.Request.Headers["Authorization"].ToString();
                    
                    if (string.IsNullOrEmpty(authHeader))
                    {
                        // No auth header, default to custom JWT
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    // Extract token from header
                    var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? authHeader.Substring(7)
                        : authHeader;

                    try
                    {
                        // Use token issuer detector to determine the scheme
                        var detector = context.RequestServices.GetRequiredService<ITokenIssuerDetector>();
                        var issuerType = detector.DetectIssuer(token);

                        return issuerType switch
                        {
                            TokenIssuerType.Keycloak => "Keycloak",
                            TokenIssuerType.CustomJwt => JwtBearerDefaults.AuthenticationScheme,
                            _ => JwtBearerDefaults.AuthenticationScheme // Default to custom JWT
                        };
                    }
                    catch (Exception ex)
                    {
                        // Log error and fallback to custom JWT
                        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning(ex, "Error detecting token issuer, defaulting to custom JWT");
                        return JwtBearerDefaults.AuthenticationScheme;
                    }
                };
            });
        }

        return builder;
    }

    /// <summary>
    /// Gets the HTTP retry policy with exponential backoff
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int maxRetries, int baseDelayMs)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts
                    Console.WriteLine($"Keycloak HTTP request retry {retryCount} after {timespan.TotalSeconds}s");
                });
    }

    /// <summary>
    /// Maps Keycloak roles to application roles
    /// </summary>
    public static void MapKeycloakRoles(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
            return;

        // Extract realm roles from Keycloak token
        var realmAccessClaim = principal.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrEmpty(realmAccessClaim))
        {
            // Parse and add realm roles as role claims
            // This is a simplified example - actual implementation would parse JSON
            // identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }

        // Extract resource roles from Keycloak token
        var resourceAccessClaim = principal.FindFirst("resource_access")?.Value;
        if (!string.IsNullOrEmpty(resourceAccessClaim))
        {
            // Parse and add resource roles as role claims
        }
    }
}
