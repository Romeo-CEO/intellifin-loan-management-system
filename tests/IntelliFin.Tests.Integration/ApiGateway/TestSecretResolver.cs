using System;
using System.Collections.Generic;
using IntelliFin.ApiGateway.Secrets;

namespace IntelliFin.Tests.Integration.ApiGateway;

public sealed class TestSecretResolver : ISecretResolver
{
    private readonly IDictionary<string, string?> _secrets;

    public TestSecretResolver(IDictionary<string, string?> secrets)
    {
        _secrets = secrets;
    }

    public string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Secret key must be provided", nameof(key));
        }

        return _secrets.TryGetValue(key, out var value) ? value : null;
    }

    public string Require(string key, string? fallback = null)
    {
        var value = Resolve(key) ?? fallback;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required secret '{key}' was not provided in test configuration.");
        }

        return value!;
    }
}
