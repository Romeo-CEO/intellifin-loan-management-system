using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IntelliFin.Shared.Observability;

public sealed class SensitiveDataRedactor
{
    private const string RedactedValue = "***";
    private readonly IReadOnlyList<Regex> _patterns;
    private readonly ISet<string> _sensitiveKeys;

    public SensitiveDataRedactor(IReadOnlyList<Regex> patterns, ISet<string> sensitiveKeys)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(sensitiveKeys);

        _patterns = patterns;
        _sensitiveKeys = sensitiveKeys;
    }

    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sanitized = value;
        foreach (var pattern in _patterns)
        {
            sanitized = pattern.Replace(sanitized, RedactedValue);
        }

        return sanitized;
    }

    public object? RedactValue(string key, object? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return value is string text ? Redact(text) : value;
        }

        if (_sensitiveKeys.Contains(key))
        {
            return RedactedValue;
        }

        return value is string stringValue ? Redact(stringValue) : value;
    }
}
