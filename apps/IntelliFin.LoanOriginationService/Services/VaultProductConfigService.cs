using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.Core;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service for loading loan product configurations from HashiCorp Vault.
/// Implements in-memory caching and EAR compliance validation.
/// </summary>
public class VaultProductConfigService : IVaultProductConfigService
{
    private readonly IVaultClient _vaultClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VaultProductConfigService> _logger;
    private const string CacheKeyPrefix = "product-config:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public VaultProductConfigService(
        IVaultClient vaultClient,
        IMemoryCache cache,
        ILogger<VaultProductConfigService> logger)
    {
        _vaultClient = vaultClient;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoanProductConfig> GetProductConfigAsync(string productCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            throw new ArgumentException("Product code cannot be null or empty", nameof(productCode));

        var cacheKey = $"{CacheKeyPrefix}{productCode}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out LoanProductConfig? cachedConfig))
        {
            _logger.LogDebug("Product config for {ProductCode} retrieved from cache", productCode);
            return cachedConfig!;
        }

        _logger.LogInformation("Loading product config for {ProductCode} from Vault", productCode);

        try
        {
            // Read from Vault KV v2
            var vaultPath = $"loan-products/{productCode}/rules";
            Secret<SecretData> secret;

            try
            {
                secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: vaultPath,
                    mountPoint: "kv");
            }
            catch (VaultApiException vex) when (vex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product configuration not found in Vault for {ProductCode} at path {VaultPath}",
                    productCode, vaultPath);
                throw new InvalidOperationException($"Product configuration not found for {productCode}");
            }

            if (secret?.Data?.Data == null || !secret.Data.Data.Any())
            {
                _logger.LogWarning("Empty product configuration in Vault for {ProductCode}", productCode);
                throw new InvalidOperationException($"Empty product configuration for {productCode}");
            }

            // Deserialize configuration
            var configJson = JsonSerializer.Serialize(secret.Data.Data);
            var config = JsonSerializer.Deserialize<LoanProductConfig>(configJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                _logger.LogError("Failed to deserialize product configuration for {ProductCode}", productCode);
                throw new InvalidOperationException($"Invalid product configuration format for {productCode}");
            }

            // Validate EAR compliance BEFORE caching
            if (config.CalculatedEAR > config.EarLimit)
            {
                _logger.LogError(
                    "EAR compliance violation for {ProductCode}: CalculatedEAR={CalculatedEAR:P2} exceeds EarLimit={EarLimit:P2}",
                    productCode, config.CalculatedEAR, config.EarLimit);

                throw new ComplianceException(productCode, config.CalculatedEAR, config.EarLimit);
            }

            _logger.LogInformation(
                "Product config for {ProductCode} loaded successfully. EAR={CalculatedEAR:P2}, Limit={EarLimit:P2}, Compliant=true",
                productCode, config.CalculatedEAR, config.EarLimit);

            // Cache the validated configuration
            _cache.Set(cacheKey, config, CacheDuration);
            _logger.LogDebug("Product config for {ProductCode} cached for {CacheDuration}", productCode, CacheDuration);

            return config;
        }
        catch (ComplianceException)
        {
            // Re-throw compliance exceptions as-is
            throw;
        }
        catch (InvalidOperationException)
        {
            // Re-throw invalid operation exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading product configuration for {ProductCode} from Vault", productCode);
            throw new InvalidOperationException($"Failed to load product configuration for {productCode}", ex);
        }
    }
}
