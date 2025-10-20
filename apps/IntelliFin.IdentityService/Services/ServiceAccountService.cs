using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AuditEvent = IntelliFin.IdentityService.Models.AuditEvent;
namespace IntelliFin.IdentityService.Services;

public class ServiceAccountService : IServiceAccountService
{
    private const string DefaultCreateReason = "service_account_created";
    private const string DefaultRotateReason = "service_account_secret_rotated";
    private const string DefaultRevokeReason = "service_account_revoked";

    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ServiceAccountService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly PasswordConfiguration _passwordConfiguration;
    private readonly ServiceAccountConfiguration _serviceAccountConfiguration;

    public ServiceAccountService(
        LmsDbContext dbContext,
        IAuditService auditService,
        ILogger<ServiceAccountService> logger,
        IHttpContextAccessor httpContextAccessor,
        IKeycloakAdminClient keycloakAdminClient,
        IOptions<PasswordConfiguration> passwordConfiguration,
        IOptions<ServiceAccountConfiguration> serviceAccountConfiguration)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _keycloakAdminClient = keycloakAdminClient;
        _passwordConfiguration = passwordConfiguration.Value;
        _serviceAccountConfiguration = serviceAccountConfiguration.Value;
    }

    public async Task<ServiceAccountDto> CreateServiceAccountAsync(ServiceAccountCreateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTime.UtcNow;
        var actor = ResolveActor(request.ActorId);
        var reason = ResolveReason(request.Reason, DefaultCreateReason);
        var scopes = NormalizeScopes(request.Scopes);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var clientId = await GenerateClientIdAsync(request.Name, ct);
            var secret = GenerateSecret();
            var secretHash = HashSecret(secret);

            var serviceAccount = new ServiceAccount
            {
                ClientId = clientId,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsActive = true,
                CreatedAtUtc = now,
                CreatedBy = actor,
                UpdatedAtUtc = now,
                UpdatedBy = actor
            };
            serviceAccount.SetScopes(scopes);

            var credential = new ServiceCredential
            {
                ServiceAccount = serviceAccount,
                SecretHash = secretHash,
                CreatedAtUtc = now,
                CreatedBy = actor,
                ExpiresAtUtc = CalculateExpiry(now)
            };

            _dbContext.ServiceAccounts.Add(serviceAccount);
            _dbContext.ServiceCredentials.Add(credential);

            await _dbContext.SaveChangesAsync(ct);

            if (_serviceAccountConfiguration.EnableKeycloakProvisioning)
            {
                await TryProvisionKeycloakAsync(serviceAccount, secret, scopes, ct);
            }

            await transaction.CommitAsync(ct);

            await _auditService.LogAsync(new AuditEvent
            {
                ActorId = actor,
                Action = "service_account.created",
                Entity = "service_account",
                EntityId = serviceAccount.Id.ToString(),
                Timestamp = now,
                Success = true,
                Result = "created",
                Details = new Dictionary<string, object>
                {
                    ["clientId"] = clientId,
                    ["name"] = serviceAccount.Name,
                    ["reason"] = reason,
                    ["scopes"] = scopes
                }
            }, ct);

            return new ServiceAccountDto
            {
                Id = serviceAccount.Id,
                ClientId = clientId,
                Name = serviceAccount.Name,
                Description = serviceAccount.Description,
                IsActive = serviceAccount.IsActive,
                CreatedAtUtc = serviceAccount.CreatedAtUtc,
                UpdatedAtUtc = serviceAccount.UpdatedAtUtc,
                DeactivatedAtUtc = serviceAccount.DeactivatedAtUtc,
                Scopes = scopes,
                Credential = new ServiceCredentialDto
                {
                    Id = credential.Id,
                    ServiceAccountId = serviceAccount.Id,
                    ClientId = clientId,
                    Secret = secret,
                    CreatedAtUtc = credential.CreatedAtUtc,
                    ExpiresAtUtc = credential.ExpiresAtUtc
                }
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to create service account {Name}", request.Name);
            throw;
        }
    }

    public async Task<ServiceCredentialDto> RotateSecretAsync(Guid serviceAccountId, CancellationToken ct = default)
    {
        var account = await _dbContext.ServiceAccounts
            .Include(x => x.Credentials)
            .FirstOrDefaultAsync(x => x.Id == serviceAccountId, ct);

        if (account is null)
        {
            throw new KeyNotFoundException($"Service account {serviceAccountId} not found");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException("Cannot rotate credentials for an inactive service account");
        }

        var now = DateTime.UtcNow;
        var actor = ResolveActor();
        var reason = ResolveReason(null, DefaultRotateReason);
        var secret = GenerateSecret();
        var secretHash = HashSecret(secret);

        var credential = new ServiceCredential
        {
            ServiceAccountId = account.Id,
            SecretHash = secretHash,
            CreatedAtUtc = now,
            CreatedBy = actor,
            ExpiresAtUtc = CalculateExpiry(now)
        };

        _dbContext.ServiceCredentials.Add(credential);
        account.UpdatedAtUtc = now;
        account.UpdatedBy = actor;

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = actor,
            Action = "service_account.secret_rotated",
            Entity = "service_account",
            EntityId = account.Id.ToString(),
            Timestamp = now,
            Success = true,
            Result = "rotated",
            Details = new Dictionary<string, object>
            {
                ["clientId"] = account.ClientId,
                ["reason"] = reason,
                ["credentialId"] = credential.Id
            }
        }, ct);

        return new ServiceCredentialDto
        {
            Id = credential.Id,
            ServiceAccountId = account.Id,
            ClientId = account.ClientId,
            Secret = secret,
            CreatedAtUtc = credential.CreatedAtUtc,
            ExpiresAtUtc = credential.ExpiresAtUtc
        };
    }

    public async Task RevokeServiceAccountAsync(Guid serviceAccountId, CancellationToken ct = default)
    {
        var account = await _dbContext.ServiceAccounts
            .Include(x => x.Credentials)
            .FirstOrDefaultAsync(x => x.Id == serviceAccountId, ct);

        if (account is null)
        {
            throw new KeyNotFoundException($"Service account {serviceAccountId} not found");
        }

        if (!account.IsActive && account.Credentials.All(c => c.RevokedAtUtc is not null))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var actor = ResolveActor();
        var reason = ResolveReason(null, DefaultRevokeReason);

        account.IsActive = false;
        account.DeactivatedAtUtc = now;
        account.UpdatedAtUtc = now;
        account.UpdatedBy = actor;

        var revokedCount = 0;
        foreach (var credential in account.Credentials.Where(c => c.RevokedAtUtc is null))
        {
            credential.RevokedAtUtc = now;
            credential.RevokedBy = actor;
            credential.RevocationReason = reason;
            revokedCount++;
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync(new AuditEvent
        {
            ActorId = actor,
            Action = "service_account.revoked",
            Entity = "service_account",
            EntityId = account.Id.ToString(),
            Timestamp = now,
            Success = true,
            Result = "revoked",
            Details = new Dictionary<string, object>
            {
                ["clientId"] = account.ClientId,
                ["revokedCredentials"] = revokedCount,
                ["reason"] = reason
            }
        }, ct);
    }

    private static IReadOnlyCollection<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return Array.Empty<string>();
        }

        var normalized = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? Array.Empty<string>() : Array.AsReadOnly(normalized);
    }

    private async Task<string> GenerateClientIdAsync(string name, CancellationToken ct)
    {
        var slug = Slugify(name);
        var attempts = 0;

        while (attempts++ < 20)
        {
            var candidate = $"{slug}-{GenerateSuffix()}";
            var exists = await _dbContext.ServiceAccounts.AnyAsync(x => x.ClientId == candidate, ct);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate unique client identifier");
    }

    private string GenerateSuffix()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var suffix = Convert.ToHexString(buffer).ToLowerInvariant();
        var length = Math.Clamp(_serviceAccountConfiguration.ClientIdSuffixLength, 4, suffix.Length);
        return suffix[..length];
    }

    private string GenerateSecret()
    {
        var length = Math.Max(_serviceAccountConfiguration.DefaultSecretLength, 32);
        var buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer);
    }

    private string HashSecret(string secret)
    {
        var workFactor = Math.Max(_passwordConfiguration.SaltRounds, 12);
        return BCrypt.Net.BCrypt.HashPassword(secret, workFactor);
    }

    private DateTime? CalculateExpiry(DateTime createdAt)
    {
        if (_serviceAccountConfiguration.CredentialExpiryDays is null or <= 0)
        {
            return null;
        }

        return createdAt.AddDays(_serviceAccountConfiguration.CredentialExpiryDays.Value);
    }

    private async Task TryProvisionKeycloakAsync(ServiceAccount account, string secret, IReadOnlyCollection<string> scopes, CancellationToken ct)
    {
        try
        {
            var registration = await _keycloakAdminClient.RegisterServiceAccountAsync(account, secret, scopes, ct);
            if (registration is null)
            {
                return;
            }

            account.KeycloakClientId = registration.ClientId;
            account.KeycloakSecretVaultPath = registration.VaultPath;
            _dbContext.ServiceAccounts.Update(account);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak provisioning failed for service account {ClientId}", account.ClientId);
        }
    }

    private string ResolveActor(string? explicitActor = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitActor))
        {
            return explicitActor.Trim();
        }

        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.Identity?.Name
                ?? "system";
        }

        return "system";
    }

    private string ResolveReason(string? explicitReason, string defaultReason)
    {
        if (!string.IsNullOrWhiteSpace(explicitReason))
        {
            return explicitReason.Trim();
        }

        var headerReason = _httpContextAccessor.HttpContext?.Request?.Headers["X-Audit-Reason"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerReason))
        {
            return headerReason.Trim();
        }

        return defaultReason;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "svc";
        }

        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-");
        slug = Regex.Replace(slug, "-+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "svc" : slug;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public async Task<ServiceAccountDto> GetServiceAccountAsync(Guid serviceAccountId, CancellationToken ct = default)
    {
        var account = await _dbContext.ServiceAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == serviceAccountId, ct);

        if (account is null)
        {
            throw new KeyNotFoundException($"Service account {serviceAccountId} not found");
        }

        return new ServiceAccountDto
        {
            Id = account.Id,
            ClientId = account.ClientId,
            Name = account.Name,
            Description = account.Description,
            IsActive = account.IsActive,
            CreatedAtUtc = account.CreatedAtUtc,
            UpdatedAtUtc = account.UpdatedAtUtc,
            DeactivatedAtUtc = account.DeactivatedAtUtc,
            Scopes = account.GetScopes(),
            Credential = null // Never return credentials in GET requests
        };
    }

    public async Task<IEnumerable<ServiceAccountDto>> ListServiceAccountsAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var query = _dbContext.ServiceAccounts.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var accounts = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return accounts.Select(account => new ServiceAccountDto
        {
            Id = account.Id,
            ClientId = account.ClientId,
            Name = account.Name,
            Description = account.Description,
            IsActive = account.IsActive,
            CreatedAtUtc = account.CreatedAtUtc,
            UpdatedAtUtc = account.UpdatedAtUtc,
            DeactivatedAtUtc = account.DeactivatedAtUtc,
            Scopes = account.GetScopes(),
            Credential = null // Never return credentials in LIST requests
        });
    }
}
