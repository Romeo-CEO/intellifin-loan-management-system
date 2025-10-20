using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IntelliFin.ApiGateway.Middleware;
using IntelliFin.ApiGateway.Options;
using IntelliFin.ApiGateway.Security;
using IntelliFin.ApiGateway.Secrets;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy;
using IntelliFin.Shared.Observability;

const string KeycloakSchemeName = "Keycloak";
const string TokenTypeItemKey = "__token_type";
const string BranchRegionHeader = "X-Branch-Region";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

var bootstrapSecretResolver = new EnvironmentSecretResolver(builder.Configuration);
builder.Services.AddSingleton<ISecretResolver>(_ => bootstrapSecretResolver);

// Configuration
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { builder.Configuration["FRONTEND_ORIGIN"] ?? "http://localhost:3000" };

// Services
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100, // requests
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
});

builder.Services.AddDbContext<LmsDbContext>((sp, options) =>
{
    var resolver = sp.GetRequiredService<ISecretResolver>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = resolver.Resolve("APIGATEWAY_DB_CONNECTION_STRING")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection must be provided via environment variables or development overrides.");

    options.UseSqlServer(connectionString);
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<IAuditService, AuditService>();

var keycloakSection = builder.Configuration.GetSection("Authentication:KeycloakJwt");
var keycloakOptions = new KeycloakJwtOptions();
keycloakSection.Bind(keycloakOptions);

var keycloakValidator = new KeycloakJwtOptionsValidator(builder.Environment);
var validationResult = keycloakValidator.Validate(nameof(KeycloakJwtOptions), keycloakOptions);
if (validationResult is { Failed: true, Failures: { Length: > 0 } failures })
{
    throw new OptionsValidationException(nameof(KeycloakJwtOptions), typeof(KeycloakJwtOptions), failures);
}

builder.Services.AddSingleton<IValidateOptions<KeycloakJwtOptions>>(keycloakValidator);
builder.Services.AddOptions<KeycloakJwtOptions>()
    .Bind(keycloakSection)
    .ValidateOnStart();

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
      .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
      .AddTransforms(builderContext =>
      {
          builderContext.AddRequestTransform(transformContext =>
          {
              var httpContext = transformContext.HttpContext;

              if (httpContext.User.Identity?.IsAuthenticated == true)
              {
                  var branchId = ResolveBranchId(httpContext);

                  if (!string.IsNullOrWhiteSpace(branchId))
                  {
                      transformContext.ProxyRequest.Headers.Remove("X-Branch-Id");
                      transformContext.ProxyRequest.Headers.Add("X-Branch-Id", branchId);
                  }

                  var branchName = ResolveStringItem(httpContext, BranchClaimItemKeys.BranchName)
                      ?? httpContext.User.FindFirstValue("branchName")
                      ?? httpContext.User.FindFirstValue("branch_name");

                  if (!string.IsNullOrWhiteSpace(branchName))
                  {
                      transformContext.ProxyRequest.Headers.Remove("X-Branch-Name");
                      transformContext.ProxyRequest.Headers.Add("X-Branch-Name", branchName);
                  }

                  var branchRegion = ResolveStringItem(httpContext, BranchClaimItemKeys.BranchRegion)
                      ?? httpContext.User.FindFirstValue("branchRegion")
                      ?? httpContext.User.FindFirstValue("branch_region");

                  if (!string.IsNullOrWhiteSpace(branchRegion))
                  {
                      transformContext.ProxyRequest.Headers.Remove(BranchRegionHeader);
                      transformContext.ProxyRequest.Headers.Add(BranchRegionHeader, branchRegion);
                  }

                  if (httpContext.Items.TryGetValue(TokenTypeItemKey, out var tokenTypeObj) && tokenTypeObj is string tokenType)
                  {
                      transformContext.ProxyRequest.Headers.Remove("X-Token-Type");
                      transformContext.ProxyRequest.Headers.Add("X-Token-Type", tokenType);
                  }
              }

              return ValueTask.CompletedTask;
          });
      });

// JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = KeycloakSchemeName;
        options.DefaultChallengeScheme = KeycloakSchemeName;
    })
    .AddJwtBearer(KeycloakSchemeName, options =>
    {
        options.Authority = keycloakOptions.Authority;
        options.RequireHttpsMetadata = keycloakOptions.RequireHttps;
        options.SaveToken = true;

        if (!string.IsNullOrWhiteSpace(keycloakOptions.MetadataAddress))
        {
            options.MetadataAddress = keycloakOptions.MetadataAddress;
        }

        if (!string.IsNullOrWhiteSpace(keycloakOptions.Audience))
        {
            options.Audience = keycloakOptions.Audience;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = keycloakOptions.Issuer!,
            ValidateAudience = true,
            ValidAudience = keycloakOptions.Audience!,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                Activity.Current?.SetTag("app.auth.token_type", KeycloakSchemeName);
                context.HttpContext.Items[TokenTypeItemKey] = KeycloakSchemeName;

                var branchIdClaim = context.Principal?.FindFirstValue("branchId") ?? context.Principal?.FindFirstValue("branch_id");
                if (!string.IsNullOrWhiteSpace(branchIdClaim))
                {
                    context.HttpContext.Items[BranchClaimItemKeys.BranchIdRaw] = branchIdClaim;
                }

                var branchNameClaim = context.Principal?.FindFirstValue("branchName") ?? context.Principal?.FindFirstValue("branch_name");
                if (!string.IsNullOrWhiteSpace(branchNameClaim))
                {
                    context.HttpContext.Items[BranchClaimItemKeys.BranchName] = branchNameClaim;
                }

                var branchRegionClaim = context.Principal?.FindFirstValue("branchRegion") ?? context.Principal?.FindFirstValue("branch_region");
                if (!string.IsNullOrWhiteSpace(branchRegionClaim))
                {
                    context.HttpContext.Items[BranchClaimItemKeys.BranchRegion] = branchRegionClaim;
                }

                await LogTokenValidationAsync(context, KeycloakSchemeName);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(KeycloakSchemeName)
        .RequireAssertion(context =>
        {
            if (context.Resource is HttpContext httpContext &&
                httpContext.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return context.User.Identity?.IsAuthenticated == true;
        })
        .Build();
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<TraceContextMiddleware>();
app.UseHttpLogging();
app.UseRateLimiter();
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseMiddleware<BranchClaimMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Proxied routes require auth
app.MapReverseProxy().RequireAuthorization();

app.MapGet("/", () => Results.Ok(new { name = "IntelliFin.ApiGateway", status = "OK" }));

app.UseHttpsRedirection();

app.Run();

static string? ResolveBranchId(HttpContext httpContext)
{
    if (httpContext.Items.TryGetValue(BranchClaimItemKeys.BranchId, out var branchIdItem))
    {
        switch (branchIdItem)
        {
            case int branchIdInt:
                return branchIdInt.ToString(CultureInfo.InvariantCulture);
            case string branchIdString when !string.IsNullOrWhiteSpace(branchIdString):
                return branchIdString;
            default:
                var itemString = branchIdItem?.ToString();
                if (!string.IsNullOrWhiteSpace(itemString))
                {
                    return itemString;
                }

                break;
        }
    }

    if (httpContext.Items.TryGetValue(BranchClaimItemKeys.BranchIdRaw, out var branchIdRaw) && branchIdRaw is string rawString && !string.IsNullOrWhiteSpace(rawString))
    {
        return rawString;
    }

    return httpContext.User.FindFirstValue("branchId") ?? httpContext.User.FindFirstValue("branch_id");
}

static string? ResolveStringItem(HttpContext httpContext, string key)
{
    if (httpContext.Items.TryGetValue(key, out var value))
    {
        if (value is string stringValue)
        {
            return string.IsNullOrWhiteSpace(stringValue) ? null : stringValue;
        }

        var converted = value?.ToString();
        if (!string.IsNullOrWhiteSpace(converted))
        {
            return converted;
        }
    }

    return null;
}

static async Task LogTokenValidationAsync(TokenValidatedContext context, string schemeName)
{
    try
    {
        var auditService = context.HttpContext.RequestServices.GetService<IAuditService>();
        if (auditService is null)
        {
            return;
        }

        var principal = context.Principal;
        if (principal is null)
        {
            return;
        }

        var actor = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.Identity?.Name
                    ?? principal.FindFirstValue("preferred_username")
                    ?? principal.FindFirstValue("sub")
                    ?? "anonymous";
        var entityId = principal.FindFirstValue("sub") ?? actor;
        var issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss) ?? principal.FindFirstValue("iss");

        var audiences = principal.Claims
            .Where(c => string.Equals(c.Type, JwtRegisteredClaimNames.Aud, StringComparison.OrdinalIgnoreCase) || c.Type == "aud")
            .Select(c => c.Value)
            .Distinct()
            .ToArray();

        var branchId = principal.FindFirstValue("branchId") ?? principal.FindFirstValue("branch_id");
        var branchName = principal.FindFirstValue("branchName") ?? principal.FindFirstValue("branch_name");
        var branchRegion = principal.FindFirstValue("branchRegion") ?? principal.FindFirstValue("branch_region");

        var auditContext = new AuditEventContext
        {
            Actor = actor,
            Action = "TOKEN_VALIDATED",
            EntityType = "Authentication",
            EntityId = entityId,
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString(),
            Source = "IntelliFin.ApiGateway",
            Category = AuditEventCategory.Authentication,
            Severity = AuditEventSeverity.Information,
            OccurredAt = DateTime.UtcNow,
            Data = new Dictionary<string, object>
            {
                ["tokenType"] = schemeName,
                ["issuer"] = issuer ?? string.Empty,
                ["audiences"] = audiences,
                ["branchId"] = branchId ?? string.Empty,
                ["branchName"] = branchName ?? string.Empty,
                ["branchRegion"] = branchRegion ?? string.Empty
            }
        };

        await auditService.LogEventAsync(auditContext);
    }
    catch (Exception ex)
    {
        var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("JwtAuditLogger");
        logger?.LogWarning(ex, "Failed to record audit event for token validation");
    }
}

public partial class Program;