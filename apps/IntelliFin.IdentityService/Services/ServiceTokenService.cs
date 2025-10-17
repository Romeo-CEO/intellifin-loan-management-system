using System.Collections.Generic;
using System.Linq;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.IdentityService.Services;

public class ServiceTokenService : IServiceTokenService
{
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IKeycloakTokenClient _keycloakTokenClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ServiceTokenService> _logger;

    public ServiceTokenService(
        LmsDbContext dbContext,
        IAuditService auditService,
        IKeycloakTokenClient keycloakTokenClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ServiceTokenService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _keycloakTokenClient = keycloakTokenClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ServiceTokenResponse> GenerateTokenAsync(ClientCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clientId = request.ClientId?.Trim();
        var clientSecret = request.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }

        var now = DateTime.UtcNow;

        var account = await _dbContext.ServiceAccounts
            .Include(x => x.Credentials)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null || !account.IsActive)
        {
            _logger.LogWarning("Token request rejected for unknown or inactive service account {ClientId}.", clientId);
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }

        var credential = account.Credentials
            .Where(c => c.RevokedAtUtc is null && (c.ExpiresAtUtc is null || c.ExpiresAtUtc > now))
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefault();

        if (credential is null)
        {
            _logger.LogWarning("Token request rejected for service account {ClientId} without active credentials.", clientId);
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }

        if (!BCrypt.Net.BCrypt.Verify(clientSecret, credential.SecretHash))
        {
            _logger.LogWarning("Token request rejected for service account {ClientId} due to invalid secret.", clientId);
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }

        var normalizedScopes = NormalizeRequestedScopes(request.Scopes);
        if (normalizedScopes is not null)
        {
            ValidateScopes(account, normalizedScopes, clientId);
        }

        var tokenResponse = await _keycloakTokenClient
            .RequestClientCredentialsTokenAsync(clientId, clientSecret, normalizedScopes, cancellationToken)
            .ConfigureAwait(false);

        await LogAuditEventAsync(account, credential, normalizedScopes, tokenResponse, cancellationToken).ConfigureAwait(false);

        var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier;
        _logger.LogInformation(
            "Issued service token for {ClientId}. CorrelationId={CorrelationId}",
            clientId,
            correlationId);

        return new ServiceTokenResponse
        {
            AccessToken = tokenResponse.AccessToken,
            ExpiresIn = tokenResponse.ExpiresIn,
            TokenType = tokenResponse.TokenType,
            Scope = tokenResponse.Scope
        };
    }

    private static string[]? NormalizeRequestedScopes(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return null;
        }

        var normalized = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }

    private void ValidateScopes(ServiceAccount account, string[] scopes, string clientId)
    {
        var allowedScopes = account.GetScopes();
        if (allowedScopes.Count == 0)
        {
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }

        var unauthorized = scopes
            .Where(scope => !allowedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (unauthorized.Length > 0)
        {
            _logger.LogWarning(
                "Token request rejected for service account {ClientId} due to unauthorized scopes: {Scopes}",
                clientId,
                string.Join(",", unauthorized));
            throw new UnauthorizedAccessException("Invalid client credentials.");
        }
    }

    private async Task LogAuditEventAsync(
        ServiceAccount account,
        ServiceCredential credential,
        string[]? scopes,
        KeycloakTokenResponse tokenResponse,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var auditEvent = new AuditEvent
        {
            ActorId = account.ClientId,
            Action = "service_account.token_issued",
            Entity = "service_account",
            EntityId = account.Id.ToString(),
            Timestamp = DateTime.UtcNow,
            Success = true,
            Result = "issued",
            SessionId = httpContext?.TraceIdentifier,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            Details = new Dictionary<string, object>
            {
                ["clientId"] = account.ClientId,
                ["credentialId"] = credential.Id,
                ["expiresIn"] = tokenResponse.ExpiresIn,
                ["tokenType"] = tokenResponse.TokenType,
                ["scopes"] = scopes ?? Array.Empty<string>()
            }
        };

        await _auditService.LogAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }
}
