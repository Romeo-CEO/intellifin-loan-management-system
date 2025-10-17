using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace IntelliFin.IdentityService.Services;

public class TokenIntrospectionService : ITokenIntrospectionService
{
    private readonly JwtConfiguration _jwtConfiguration;
    private readonly AuthorizationConfiguration _authorizationConfiguration;
    private readonly RedisConfiguration _redisConfiguration;
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenIntrospectionService> _logger;
    private readonly HashSet<string> _trustedIssuers;
    private readonly TokenValidationParameters _localValidationParameters;
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configurationManagers = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public TokenIntrospectionService(
        IOptions<JwtConfiguration> jwtOptions,
        IOptions<AuthorizationConfiguration> authorizationOptions,
        IOptions<RedisConfiguration> redisOptions,
        LmsDbContext dbContext,
        IAuditService auditService,
        IConnectionMultiplexer connectionMultiplexer,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenIntrospectionService> logger)
    {
        _jwtConfiguration = jwtOptions.Value;
        _authorizationConfiguration = authorizationOptions.Value;
        _redisConfiguration = redisOptions.Value;
        _dbContext = dbContext;
        _auditService = auditService;
        _connectionMultiplexer = connectionMultiplexer;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _trustedIssuers = new HashSet<string>(_authorizationConfiguration.TrustedIssuers ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
        {
            _jwtConfiguration.Issuer
        };

        _localValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = _jwtConfiguration.Issuer,
            ValidateIssuer = _jwtConfiguration.ValidateIssuer,
            ValidAudience = _jwtConfiguration.Audience,
            ValidateAudience = _jwtConfiguration.ValidateAudience,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = _jwtConfiguration.ValidateIssuerSigningKey,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfiguration.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(_jwtConfiguration.ClockSkew)
        };
    }

    public async Task<IntrospectionResponse> IntrospectAsync(IntrospectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new ArgumentException("Token must be provided for introspection.", nameof(request));
        }

        JwtSecurityToken rawToken;
        try
        {
            rawToken = new JwtSecurityTokenHandler().ReadJwtToken(request.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse token for introspection");
            return await BuildInactiveResponseAsync(null, null, "unparseable", cancellationToken).ConfigureAwait(false);
        }

        var issuer = rawToken.Issuer;
        if (string.IsNullOrWhiteSpace(issuer) || !_trustedIssuers.Contains(issuer))
        {
            _logger.LogWarning("Token introspection rejected due to unknown issuer {Issuer}", issuer);
            throw new UnknownIssuerException(issuer ?? "unknown");
        }

        var validationParameters = await GetValidationParametersAsync(issuer, cancellationToken).ConfigureAwait(false);

        TokenValidationResult validationResult;
        try
        {
            validationResult = await _tokenHandler.ValidateTokenAsync(request.Token, validationParameters);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token introspection validation failed");
            return await BuildInactiveResponseAsync(rawToken, issuer, ex.GetType().Name, cancellationToken).ConfigureAwait(false);
        }

        if (!validationResult.IsValid || validationResult.SecurityToken is null)
        {
            _logger.LogInformation("Token introspection failed validation for issuer {Issuer}", issuer);
            return await BuildInactiveResponseAsync(rawToken, issuer, "invalid_signature", cancellationToken).ConfigureAwait(false);
        }

        var claimsIdentity = validationResult.ClaimsIdentity;
        var claims = claimsIdentity?.Claims ?? Array.Empty<Claim>();
        var subject = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
        var username = ResolveUsername(claims);
        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == JwtRegisteredClaimNames.Email)?.Value;
        var roles = ResolveRoles(claims);
        var permissions = ResolvePermissions(claims);
        var branchId = ResolveGuidClaim(claims, "branch_id");
        var tenantId = ResolveGuidClaim(claims, "tenant_id");
        var tokenId = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        var issuedAt = ResolveLongClaim(claims, JwtRegisteredClaimNames.Iat);
        var expiresAt = ResolveLongClaim(claims, JwtRegisteredClaimNames.Exp);

        if (!string.IsNullOrEmpty(tokenId))
        {
            var isRevoked = await IsTokenRevokedAsync(tokenId, cancellationToken).ConfigureAwait(false);
            if (isRevoked)
            {
                return await BuildInactiveResponseAsync(rawToken, issuer, "revoked", cancellationToken, subject, tokenId).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrEmpty(subject))
        {
            var enrichment = await EnrichFromDirectoryAsync(subject, cancellationToken).ConfigureAwait(false);
            if (enrichment is not null)
            {
                roles = roles.Union(enrichment.Roles, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                permissions = permissions.Union(enrichment.Permissions, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                if (branchId is null && enrichment.BranchId is not null)
                {
                    branchId = enrichment.BranchId;
                }

                if (tenantId is null && enrichment.TenantId is not null)
                {
                    tenantId = enrichment.TenantId;
                }

                if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(enrichment.Username))
                {
                    username = enrichment.Username;
                }

                if (string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(enrichment.Email))
                {
                    email = enrichment.Email;
                }
            }
        }

        var response = new IntrospectionResponse
        {
            Active = true,
            Subject = subject,
            Username = username,
            Email = email,
            Roles = roles,
            Permissions = permissions,
            BranchId = branchId,
            TenantId = tenantId,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Issuer = issuer
        };

        await LogAuditAsync(subject ?? username ?? "unknown", tokenId, issuer, true, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token introspection succeeded for subject {Subject} (issuer {Issuer})", subject, issuer);

        return response;
    }
    private async Task<IntrospectionResponse> BuildInactiveResponseAsync(
        JwtSecurityToken? rawToken,
        string? issuer,
        string reason,
        CancellationToken cancellationToken,
        string? subject = null,
        string? tokenId = null)
    {
        await LogAuditAsync(subject ?? rawToken?.Subject ?? "unknown", tokenId ?? rawToken?.Id, issuer, false, cancellationToken, reason).ConfigureAwait(false);

        return new IntrospectionResponse
        {
            Active = false,
            Subject = rawToken?.Subject,
            Issuer = issuer,
            ExpiresAt = ResolveUnixTime(rawToken?.ValidTo),
            IssuedAt = ResolveUnixTime(rawToken?.ValidFrom),
            Username = subject,
            Roles = Array.Empty<string>(),
            Permissions = Array.Empty<string>()
        };
    }

    private async Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken cancellationToken)
    {
        try
        {
            var database = _connectionMultiplexer.GetDatabase(_redisConfiguration.Database);
            var revokedKey = new RedisKey[]
            {
                new RedisKey($"{_redisConfiguration.KeyPrefix}revoked_token:{tokenId}"),
                new RedisKey($"{_redisConfiguration.KeyPrefix}deny:{tokenId}")
            };

            foreach (var key in revokedKey)
            {
                if (await database.KeyExistsAsync(key).ConfigureAwait(false))
                {
                    _logger.LogInformation("Token {TokenId} is deny-listed in Redis", tokenId);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed checking Redis denylist for token {TokenId}", tokenId);
        }

        return await _dbContext.TokenRevocations
            .AsNoTracking()
            .AnyAsync(x => x.TokenId == tokenId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(string[] Roles, string[] Permissions, Guid? BranchId, Guid? TenantId, string? Username, string? Email)?> EnrichFromDirectoryAsync(string subject, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles.Where(ur => ur.IsActive))
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == subject, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        var activeRoles = user.UserRoles
            .Where(ur => ur.IsActive && ur.Role is not null && ur.Role.IsActive)
            .ToList();

        var roleNames = activeRoles
            .Select(ur => ur.Role!.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissionNames = activeRoles
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.IsActive && rp.Permission is not null && rp.Permission.IsActive)
            .Select(rp => rp.Permission!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Guid? branchId = null;
        if (!string.IsNullOrWhiteSpace(user.BranchId) && Guid.TryParse(user.BranchId, out var parsedBranch))
        {
            branchId = parsedBranch;
        }

        Guid? tenantId = ResolveTenantId(user);

        return (roleNames, permissionNames, branchId, tenantId, user.Username, user.Email);
    }

    private Guid? ResolveTenantId(User user)
    {
        if (user.Metadata is null || user.Metadata.Count == 0)
        {
            return null;
        }

        if (user.Metadata.TryGetValue("tenantId", out var tenantValue) || user.Metadata.TryGetValue("tenant_id", out tenantValue))
        {
            if (tenantValue is not null && Guid.TryParse(tenantValue.ToString(), out var tenantGuid))
            {
                return tenantGuid;
            }
        }

        return null;
    }

    private async Task<TokenValidationParameters> GetValidationParametersAsync(string issuer, CancellationToken cancellationToken)
    {
        if (string.Equals(issuer, _jwtConfiguration.Issuer, StringComparison.OrdinalIgnoreCase))
        {
            return _localValidationParameters;
        }

        var configurationManager = _configurationManagers.GetOrAdd(issuer, key =>
        {
            var metadataAddress = ResolveMetadataAddress(key);
            var httpClient = _httpClientFactory.CreateClient("oidc-metadata");
            var retriever = new HttpDocumentRetriever(httpClient)
            {
                RequireHttps = _authorizationConfiguration.RequireHttpsMetadata
            };

            var manager = new ConfigurationManager<OpenIdConnectConfiguration>(metadataAddress, new OpenIdConnectConfigurationRetriever(), retriever)
            {
                AutomaticRefreshInterval = TimeSpan.FromMinutes(Math.Max(5, _authorizationConfiguration.MetadataCacheMinutes)),
                RefreshInterval = TimeSpan.FromMinutes(1)
            };

            return manager;
        });

        var configuration = await configurationManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var audiences = _authorizationConfiguration.AllowedAudiences ?? Array.Empty<string>();

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = audiences.Length > 0,
            ValidAudiences = audiences.Length > 0 ? audiences : null,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(_jwtConfiguration.ClockSkew)
        };
    }

    private string ResolveMetadataAddress(string issuer)
    {
        if (_authorizationConfiguration.IssuerMetadata is not null && _authorizationConfiguration.IssuerMetadata.TryGetValue(issuer, out var configured))
        {
            return configured;
        }

        return issuer.TrimEnd('/') + "/.well-known/openid-configuration";
    }

    private async Task LogAuditAsync(string actor, string? tokenId, string? issuer, bool success, CancellationToken cancellationToken, string? reason = null)
    {
        var details = new Dictionary<string, object>
        {
            ["issuer"] = issuer ?? "unknown",
            ["result"] = success ? "active" : "inactive"
        };

        if (!string.IsNullOrEmpty(tokenId))
        {
            details["jti"] = tokenId;
        }

        if (!string.IsNullOrEmpty(reason))
        {
            details["reason"] = reason;
        }

        var auditEvent = new AuditEvent
        {
            ActorId = actor,
            Action = "token.introspection",
            Entity = "token",
            EntityId = tokenId,
            Timestamp = DateTime.UtcNow,
            Success = success,
            Result = success ? "active" : "inactive",
            Details = details
        };

        await _auditService.LogAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveUsername(IEnumerable<Claim> claims)
    {
        return claims.FirstOrDefault(c => c.Type == "preferred_username" || c.Type == ClaimTypes.Name || c.Type == "username")?.Value
            ?? claims.FirstOrDefault(c => c.Type == "client_id")?.Value
            ?? string.Empty;
    }

    private static string[] ResolveRoles(IEnumerable<Claim> claims)
    {
        var roleClaims = claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
            .SelectMany(c => c.Type == "roles" ? c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : new[] { c.Value })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roleClaims;
    }

    private static string[] ResolvePermissions(IEnumerable<Claim> claims)
    {
        return claims
            .Where(c => c.Type == "permission" || c.Type == "permissions")
            .SelectMany(c => c.Type == "permissions" ? c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : new[] { c.Value })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Guid? ResolveGuidClaim(IEnumerable<Claim> claims, string type)
    {
        var value = claims.FirstOrDefault(c => c.Type == type)?.Value;
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static long? ResolveLongClaim(IEnumerable<Claim> claims, string type)
    {
        var value = claims.FirstOrDefault(c => c.Type == type)?.Value;
        if (long.TryParse(value, out var longValue))
        {
            return longValue;
        }

        return null;
    }

    private static long? ResolveUnixTime(DateTime? dateTime)
    {
        if (dateTime is null)
        {
            return null;
        }

        var utc = DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc);
        var unixTime = new DateTimeOffset(utc).ToUnixTimeSeconds();
        return unixTime;
    }
}
