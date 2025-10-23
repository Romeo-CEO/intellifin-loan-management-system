using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Models;
using IntelliFin.ClientManagement.Infrastructure.Configuration;
using IntelliFin.ClientManagement.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace IntelliFin.ClientManagement.Infrastructure.VaultClient;

/// <summary>
/// Retrieves risk scoring configuration from HashiCorp Vault
/// Implements caching and change detection
/// </summary>
public class VaultRiskConfigProvider : IRiskConfigProvider
{
    private readonly ILogger<VaultRiskConfigProvider> _logger;
    private readonly VaultOptions _options;
    private readonly IVaultClient? _vaultClient;
    private readonly List<Action<RiskScoringConfig>> _callbacks = new();
    private readonly SemaphoreSlim _configLock = new(1, 1);
    
    private RiskScoringConfig? _cachedConfig;
    private DateTime _lastRetrieved = DateTime.MinValue;

    public VaultRiskConfigProvider(
        ILogger<VaultRiskConfigProvider> logger,
        IOptions<VaultOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Initialize Vault client if enabled
        if (_options.Enabled && _options.IsValid())
        {
            try
            {
                _vaultClient = CreateVaultClient();
                _logger.LogInformation("Vault client initialized for risk scoring configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Vault client, will use fallback configuration");
                _vaultClient = null;
            }
        }
        else
        {
            _logger.LogWarning("Vault integration disabled or invalid configuration, using fallback");
        }
    }

    public async Task<Result<RiskScoringConfig>> GetCurrentConfigAsync()
    {
        // If Vault disabled or client not initialized, return fallback
        if (!_options.Enabled || _vaultClient == null)
        {
            _logger.LogDebug("Returning fallback configuration (Vault disabled)");
            return Result<RiskScoringConfig>.Success(GetFallbackConfig());
        }

        await _configLock.WaitAsync();
        try
        {
            // Return cached config if recent (within polling interval)
            var cacheAge = DateTime.UtcNow - _lastRetrieved;
            if (_cachedConfig != null && cacheAge.TotalSeconds < _options.PollingIntervalSeconds)
            {
                _logger.LogDebug("Returning cached configuration (age: {Age}s)", (int)cacheAge.TotalSeconds);
                return Result<RiskScoringConfig>.Success(_cachedConfig);
            }

            // Fetch from Vault
            return await RefreshConfigAsync();
        }
        finally
        {
            _configLock.Release();
        }
    }

    public async Task<Result<RiskScoringConfig>> RefreshConfigAsync()
    {
        if (_vaultClient == null)
        {
            _logger.LogWarning("Vault client not initialized, using fallback configuration");
            return Result<RiskScoringConfig>.Success(GetFallbackConfig());
        }

        try
        {
            _logger.LogInformation("Fetching risk scoring configuration from Vault: {Path}", _options.RiskScoringConfigPath);

            // Read from Vault KV v2
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                path: "client-management/risk-scoring-rules",
                mountPoint: _options.MountPoint);

            if (secret?.Data?.Data == null)
            {
                _logger.LogWarning("Empty configuration received from Vault, using fallback");
                return Result<RiskScoringConfig>.Success(GetFallbackConfig());
            }

            // Parse configuration
            var configJson = JsonSerializer.Serialize(secret.Data.Data);
            var config = JsonSerializer.Deserialize<RiskScoringConfig>(configJson);

            if (config == null || !config.IsValid())
            {
                _logger.LogError("Invalid configuration received from Vault, using fallback");
                return Result<RiskScoringConfig>.Success(GetFallbackConfig());
            }

            // Calculate checksum if not provided
            if (string.IsNullOrEmpty(config.Checksum))
            {
                config.Checksum = CalculateChecksum(configJson);
            }

            // Check if configuration changed
            var isNewConfig = _cachedConfig == null ||
                            _cachedConfig.Version != config.Version ||
                            _cachedConfig.Checksum != config.Checksum;

            if (isNewConfig)
            {
                _logger.LogInformation(
                    "Configuration change detected: {OldVersion} -> {NewVersion}",
                    _cachedConfig?.Version ?? "none", config.Version);

                // Notify callbacks
                foreach (var callback in _callbacks)
                {
                    try
                    {
                        callback(config);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in configuration change callback");
                    }
                }
            }

            _cachedConfig = config;
            _lastRetrieved = DateTime.UtcNow;

            _logger.LogInformation(
                "Risk scoring configuration loaded: Version={Version}, Rules={RuleCount}",
                config.Version, config.Rules.Count);

            return Result<RiskScoringConfig>.Success(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration from Vault");

            if (_options.UseFallbackOnError)
            {
                _logger.LogWarning("Using fallback configuration due to Vault error");
                return Result<RiskScoringConfig>.Success(GetFallbackConfig());
            }

            return Result<RiskScoringConfig>.Failure($"Failed to retrieve Vault configuration: {ex.Message}");
        }
    }

    public void RegisterConfigChangeCallback(Action<RiskScoringConfig> callback)
    {
        _callbacks.Add(callback);
        _logger.LogDebug("Configuration change callback registered (total: {Count})", _callbacks.Count);
    }

    public Task<bool> ValidateConfigAsync(RiskScoringConfig config)
    {
        if (config == null)
            return Task.FromResult(false);

        if (!config.IsValid())
            return Task.FromResult(false);

        // Additional validation
        if (config.Rules.Count == 0)
        {
            _logger.LogWarning("Configuration has no rules defined");
            return Task.FromResult(false);
        }

        if (config.Thresholds.Count < 3)
        {
            _logger.LogWarning("Configuration missing required thresholds (Low, Medium, High)");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public RiskScoringConfig GetFallbackConfig()
    {
        _logger.LogDebug("Using fallback risk scoring configuration");

        return new RiskScoringConfig
        {
            Version = "fallback-1.0.0",
            Checksum = "fallback",
            LastModified = DateTime.UtcNow,
            Rules = new Dictionary<string, RiskRule>
            {
                ["kyc_incomplete"] = new()
                {
                    Name = "KYC Incomplete",
                    Description = "Client has incomplete KYC documentation",
                    Points = 20,
                    Condition = "kycComplete == false",
                    IsEnabled = true,
                    Priority = 1,
                    Category = "KYC"
                },
                ["aml_high_risk"] = new()
                {
                    Name = "AML High Risk",
                    Description = "AML screening returned high risk",
                    Points = 50,
                    Condition = "amlRiskLevel == \"High\"",
                    IsEnabled = true,
                    Priority = 0,
                    Category = "AML"
                },
                ["sanctions_hit"] = new()
                {
                    Name = "Sanctions Hit",
                    Description = "Client matches sanctions list",
                    Points = 75,
                    Condition = "hasSanctionsHit == true",
                    IsEnabled = true,
                    Priority = 0,
                    Category = "AML"
                },
                ["pep_active"] = new()
                {
                    Name = "Active PEP",
                    Description = "Client is a Politically Exposed Person",
                    Points = 30,
                    Condition = "isPep == true",
                    IsEnabled = true,
                    Priority = 2,
                    Category = "AML"
                },
                ["young_client"] = new()
                {
                    Name = "Young Client",
                    Description = "Client is under 25 years old",
                    Points = 10,
                    Condition = "age < 25",
                    IsEnabled = true,
                    Priority = 5,
                    Category = "Profile"
                }
            },
            Thresholds = new Dictionary<string, RiskThreshold>
            {
                ["low"] = new()
                {
                    Rating = "Low",
                    MinScore = 0,
                    MaxScore = 25,
                    Description = "Low risk - standard monitoring"
                },
                ["medium"] = new()
                {
                    Rating = "Medium",
                    MinScore = 26,
                    MaxScore = 50,
                    Description = "Medium risk - enhanced monitoring"
                },
                ["high"] = new()
                {
                    Rating = "High",
                    MinScore = 51,
                    MaxScore = 100,
                    Description = "High risk - requires EDD"
                }
            },
            Options = new RiskScoringOptions
            {
                MaxScore = 100,
                DefaultRating = "Medium",
                EnableAuditLogging = true,
                StopOnError = false
            }
        };
    }

    private IVaultClient CreateVaultClient()
    {
        IAuthMethodInfo authMethod = _options.AuthMethod.ToLowerInvariant() switch
        {
            "token" => new TokenAuthMethodInfo(_options.Token),
            // JWT and other methods would be implemented here
            _ => throw new NotSupportedException($"Auth method not supported: {_options.AuthMethod}")
        };

        var vaultClientSettings = new VaultClientSettings(
            _options.Address,
            authMethod)
        {
            VaultServiceTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        return new VaultSharp.VaultClient(vaultClientSettings);
    }

    private static string CalculateChecksum(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return $"sha256:{Convert.ToHexString(hashBytes).ToLowerInvariant()}";
    }
}
