using System.Net.Http.Json;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace IntelliFin.CreditAssessmentService.Services;

/// <summary>
/// Retrieves rule and threshold configuration from HashiCorp Vault with caching and fallback support.
/// </summary>
public sealed class VaultConfigService : IVaultConfigService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<VaultConfigService> _logger;
    private readonly VaultRuleOptions _options;
    private readonly IConfiguration _configuration;

    private static readonly string RuleCacheKey = "vault_rules";
    private static readonly string ThresholdCacheKey = "vault_thresholds";
    private static readonly string TransUnionCacheKey = "vault_transunion";

    public VaultConfigService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IOptions<VaultRuleOptions> options,
        IConfiguration configuration,
        ILogger<VaultConfigService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<VaultRuleConfiguration> GetRuleConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(RuleCacheKey, out VaultRuleConfiguration cached))
        {
            return cached;
        }

        var configuration = await LoadFromVaultAsync<VaultRuleConfiguration>(_options.RulePath, cancellationToken)
            ?? LoadFallbackConfiguration<VaultRuleConfiguration>("AssessmentRules:Default")
            ?? new VaultRuleConfiguration
            {
                Rules = new List<VaultRule>
                {
                    new()
                    {
                        Key = "debt_to_income",
                        Expression = "context.DebtToIncomeRatio <= thresholds.DebtToIncomeThreshold",
                        Weight = 0.35m,
                        Category = "Affordability"
                    },
                    new()
                    {
                        Key = "credit_score",
                        Expression = "context.BureauScore",
                        Weight = 0.40m,
                        Category = "CreditHistory"
                    },
                    new()
                    {
                        Key = "employment_stability",
                        Expression = "context.FinancialMetrics['employment_months']",
                        Weight = 0.25m,
                        Category = "Employment"
                    }
                }
            };

        _memoryCache.Set(RuleCacheKey, configuration, TimeSpan.FromMinutes(_options.RefreshIntervalMinutes));
        return configuration;
    }

    public async Task<VaultThresholdConfiguration> GetThresholdConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(ThresholdCacheKey, out VaultThresholdConfiguration cached))
        {
            return cached;
        }

        var configuration = await LoadFromVaultAsync<VaultThresholdConfiguration>(_options.ThresholdPath, cancellationToken)
            ?? LoadFallbackConfiguration<VaultThresholdConfiguration>("AssessmentRules:Thresholds")
            ?? new VaultThresholdConfiguration();

        _memoryCache.Set(ThresholdCacheKey, configuration, TimeSpan.FromMinutes(_options.RefreshIntervalMinutes));
        return configuration;
    }

    public async Task<VaultTransUnionCredential> GetTransUnionCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(TransUnionCacheKey, out VaultTransUnionCredential cached))
        {
            return cached;
        }

        var credentials = await LoadFromVaultAsync<VaultTransUnionCredential>(_options.TransUnionSecretPath, cancellationToken)
            ?? LoadFallbackConfiguration<VaultTransUnionCredential>("TransUnion:FallbackCredentials")
            ?? new VaultTransUnionCredential();

        _memoryCache.Set(TransUnionCacheKey, credentials, TimeSpan.FromMinutes(_options.RefreshIntervalMinutes));
        return credentials;
    }

    private async Task<T?> LoadFromVaultAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Vault integration disabled. Using fallback configuration for {Path}", path);
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("vault");
            var response = await client.GetAsync($"v1/{path}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vault returned non-success status {StatusCode} for path {Path}", response.StatusCode, path);
                return null;
            }

            var wrapper = await response.Content.ReadFromJsonAsync<VaultSecretWrapper<T>>(cancellationToken: cancellationToken);
            if (wrapper?.Data is null)
            {
                _logger.LogWarning("Vault path {Path} returned empty payload", path);
                return null;
            }

            return wrapper.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load Vault configuration from {Path}", path);
            return null;
        }
    }

    private T? LoadFallbackConfiguration<T>(string configurationSection) where T : class
    {
        var section = _configuration.GetSection(configurationSection);
        if (!section.Exists())
        {
            return null;
        }

        return section.Get<T>();
    }

    private sealed class VaultSecretWrapper<T>
    {
        public T? Data { get; set; }
    }
}
