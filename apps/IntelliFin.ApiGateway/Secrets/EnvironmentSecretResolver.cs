using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntelliFin.ApiGateway.Secrets;

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnvironmentSecretResolver> _logger;

    public EnvironmentSecretResolver(IConfiguration configuration, ILogger<EnvironmentSecretResolver>? logger = null)
    {
        _configuration = configuration;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EnvironmentSecretResolver>.Instance;
    }

    public string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key must be provided", nameof(key));
        }

        var envValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        var filePath = Environment.GetEnvironmentVariable($"{key}_FILE");
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try
            {
                return File.ReadAllText(filePath).Trim();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to read secret file for key {Key}", key);
            }
        }

        var configured = _configuration[$"Secrets:{key}"] ?? _configuration[key];
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    public string Require(string key, string? fallback = null)
    {
        var value = Resolve(key) ?? fallback;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required secret '{key}' was not provided.");
        }

        return value!;
    }
}
