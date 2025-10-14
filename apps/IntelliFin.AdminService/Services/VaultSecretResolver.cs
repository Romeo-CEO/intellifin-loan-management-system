using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.Database;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;

namespace IntelliFin.AdminService.Services;

public sealed class VaultSecretResolver : IVaultSecretResolver
{
    private static readonly string AdminCacheKey = "vault:sql:admin";
    private static readonly string FinancialCacheKey = "vault:sql:financial";
    private static readonly string IdentityCacheKey = "vault:sql:identity";
    private static readonly string RabbitCacheKey = "vault:rabbitmq";
    private static readonly string MinioCacheKey = "vault:minio";

    private readonly IMemoryCache _cache;
    private readonly IVaultClient _vaultClient;
    private readonly VaultOptions _options;
    private readonly ILogger<VaultSecretResolver> _logger;
    private readonly IConfiguration _configuration;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly TimeSpan _defaultTtl;

    public VaultSecretResolver(
        IMemoryCache cache,
        IVaultClient vaultClient,
        IOptions<VaultOptions> options,
        ILogger<VaultSecretResolver> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _vaultClient = vaultClient;
        _options = options.Value;
        _logger = logger;
        _configuration = configuration;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * 200),
                (exception, _, attempt, _) =>
                {
                    _logger.LogWarning(exception, "Retrying Vault secret resolution (attempt {Attempt})", attempt);
                });

        _defaultTtl = TimeSpan.FromSeconds(Math.Clamp(_options.DefaultSecretCacheSeconds, 30, 3_600));

        if (_options.Enabled && string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new InvalidOperationException("Vault token must be configured when Vault integration is enabled.");
        }
    }

    public Task EnsureSecretsAvailableAsync(CancellationToken cancellationToken)
    {
        return EnsureSecretsInternalAsync(cancellationToken);
    }

    public Task<string> GetAdminConnectionStringAsync(CancellationToken cancellationToken)
        => ResolveSqlConnectionStringAsync(
            AdminCacheKey,
            "Default",
            _options.SecretPaths.AdminDatabaseRole,
            "ADMIN_DB",
            cancellationToken);

    public Task<string> GetFinancialConnectionStringAsync(CancellationToken cancellationToken)
        => ResolveSqlConnectionStringAsync(
            FinancialCacheKey,
            "FinancialService",
            _options.SecretPaths.FinancialDatabaseRole,
            "FINANCIAL_DB",
            cancellationToken);

    public Task<string> GetIdentityConnectionStringAsync(CancellationToken cancellationToken)
        => ResolveSqlConnectionStringAsync(
            IdentityCacheKey,
            "IdentityDb",
            _options.SecretPaths.IdentityDatabaseRole,
            "IDENTITY_DB",
            cancellationToken);

    public async Task<RabbitMqCredentials> GetRabbitMqCredentialsAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return ResolveRabbitMqFromFallback();
        }

        var entry = await _cache.GetOrCreateAsync(RabbitCacheKey, async cacheEntry =>
        {
            var credentials = await ResolveRabbitMqFromVaultAsync(cancellationToken).ConfigureAwait(false);
            cacheEntry.AbsoluteExpirationRelativeToNow = _defaultTtl;
            cacheEntry.Priority = CacheItemPriority.High;
            return credentials;
        }).ConfigureAwait(false);

        return entry!;
    }

    public async Task<MinioCredentials> GetMinioCredentialsAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return ResolveMinioFromFallback();
        }

        var entry = await _cache.GetOrCreateAsync(MinioCacheKey, async cacheEntry =>
        {
            var credentials = await ResolveMinioFromVaultAsync(cancellationToken).ConfigureAwait(false);
            cacheEntry.AbsoluteExpirationRelativeToNow = _defaultTtl;
            cacheEntry.Priority = CacheItemPriority.High;
            return credentials;
        }).ConfigureAwait(false);

        return entry!;
    }

    private async Task EnsureSecretsInternalAsync(CancellationToken cancellationToken)
    {
        await GetAdminConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        await GetFinancialConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        await GetIdentityConnectionStringAsync(cancellationToken).ConfigureAwait(false);
        await GetRabbitMqCredentialsAsync(cancellationToken).ConfigureAwait(false);
        await GetMinioCredentialsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolveSqlConnectionStringAsync(
        string cacheKey,
        string connectionName,
        string vaultRoleName,
        string fallbackPrefix,
        CancellationToken cancellationToken)
    {
        var baseConnectionString = _configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} must be configured.");

        if (!_options.Enabled)
        {
            return ResolveConnectionStringFromFallback(baseConnectionString, fallbackPrefix);
        }

        var secret = await _cache.GetOrCreateAsync(cacheKey, async cacheEntry =>
        {
            var credentials = await ResolveDatabaseCredentialsAsync(vaultRoleName, cancellationToken).ConfigureAwait(false);
            var ttl = CalculateCacheLifetime(credentials.LeaseDurationSeconds);
            cacheEntry.AbsoluteExpirationRelativeToNow = ttl;
            cacheEntry.Priority = CacheItemPriority.High;
            return credentials;
        }).ConfigureAwait(false);

        var username = secret!.Data.Username;
        var password = secret.Data.Password;

        return BuildConnectionString(baseConnectionString, username, password);
    }

    private async Task<Secret<UsernamePasswordCredentials>> ResolveDatabaseCredentialsAsync(
        string vaultRoleName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching database credentials from Vault role {Role}", vaultRoleName);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var credentials = await _vaultClient.V1.Secrets.Database
                .GetCredentialsAsync(vaultRoleName.Trim('/'), _options.SecretsEnginePath)
                .ConfigureAwait(false);

            if (credentials?.Data is null || string.IsNullOrWhiteSpace(credentials.Data.Username) || string.IsNullOrWhiteSpace(credentials.Data.Password))
            {
                throw new InvalidOperationException($"Vault role '{vaultRoleName}' returned empty credentials.");
            }

            return credentials;
        }).ConfigureAwait(false);
    }

    private async Task<RabbitMqCredentials> ResolveRabbitMqFromVaultAsync(CancellationToken cancellationToken)
    {
        var path = _options.SecretPaths.RabbitMq.Path;
        _logger.LogInformation("Retrieving RabbitMQ credentials from Vault path {Path}", path);

        var secret = await _retryPolicy.ExecuteAsync(async () =>
        {
            var result = await _vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync<Dictionary<string, string>>(path.Trim('/'), mountPoint: _options.SecretPaths.RabbitMq.MountPoint)
                .ConfigureAwait(false);

            return result;
        }).ConfigureAwait(false);

        var data = secret?.Data?.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var username = TryGetDictionaryValue(data, "username") ?? throw new InvalidOperationException($"Vault secret at '{path}' did not contain 'username'.");
        var password = TryGetDictionaryValue(data, "password") ?? throw new InvalidOperationException($"Vault secret at '{path}' did not contain 'password'.");

        return new RabbitMqCredentials(username, password);
    }

    private async Task<MinioCredentials> ResolveMinioFromVaultAsync(CancellationToken cancellationToken)
    {
        var path = _options.SecretPaths.Minio.Path;
        _logger.LogInformation("Retrieving MinIO credentials from Vault path {Path}", path);

        var secret = await _retryPolicy.ExecuteAsync(async () =>
        {
            var result = await _vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync<Dictionary<string, string>>(path.Trim('/'), mountPoint: _options.SecretPaths.Minio.MountPoint)
                .ConfigureAwait(false);

            return result;
        }).ConfigureAwait(false);

        var data = secret?.Data?.Data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var accessKey = TryGetDictionaryValue(data, "accessKey") ?? TryGetDictionaryValue(data, "access_key") ?? throw new InvalidOperationException($"Vault secret at '{path}' did not contain 'accessKey'.");
        var secretKey = TryGetDictionaryValue(data, "secretKey") ?? TryGetDictionaryValue(data, "secret_key") ?? throw new InvalidOperationException($"Vault secret at '{path}' did not contain 'secretKey'.");

        return new MinioCredentials(accessKey, secretKey);
    }

    private RabbitMqCredentials ResolveRabbitMqFromFallback()
    {
        var username = ResolveRequiredSecret("AUDIT_RABBITMQ", "Username");
        var password = ResolveRequiredSecret("AUDIT_RABBITMQ", "Password");
        return new RabbitMqCredentials(username, password);
    }

    private MinioCredentials ResolveMinioFromFallback()
    {
        var accessKey = ResolveRequiredSecret("MINIO", "AccessKey");
        var secretKey = ResolveRequiredSecret("MINIO", "SecretKey");
        return new MinioCredentials(accessKey, secretKey);
    }

    private string ResolveConnectionStringFromFallback(string baseConnectionString, string prefix)
    {
        var explicitConnection = ResolveOptionalSecret(prefix, "ConnectionString");
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            return explicitConnection!;
        }

        var username = ResolveRequiredSecret(prefix, "Username");
        var password = ResolveRequiredSecret(prefix, "Password");
        return BuildConnectionString(baseConnectionString, username, password);
    }

    private static string BuildConnectionString(string baseConnectionString, string username, string password)
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            UserID = username,
            Password = password,
            IntegratedSecurity = false
        };

        return builder.ConnectionString;
    }

    private TimeSpan CalculateCacheLifetime(int leaseDurationSeconds)
    {
        if (leaseDurationSeconds <= 0)
        {
            return _defaultTtl;
        }

        var lease = TimeSpan.FromSeconds(leaseDurationSeconds);
        var adjusted = TimeSpan.FromSeconds(Math.Max(30, lease.TotalSeconds * 0.8));
        return adjusted < _defaultTtl ? adjusted : _defaultTtl;
    }

    private string ResolveRequiredSecret(string prefix, string field)
    {
        var value = ResolveOptionalSecret(prefix, field);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required secret value for {prefix}:{field}.");
        }

        return value!;
    }

    private string? ResolveOptionalSecret(string prefix, string field)
    {
        var envKey = $"{prefix}_{field}".Replace(" ", string.Empty).ToUpperInvariant();
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        var configKey = $"SecretFallbacks:{prefix}:{field}";
        var configValue = _configuration[configKey];
        return string.IsNullOrWhiteSpace(configValue) ? null : configValue;
    }

    private static string? TryGetDictionaryValue(IDictionary<string, string> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value : null;
    }
}
