using System.Text.Json;
using Zeebe.Client.Api.Responses;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Helper class for parsing Zeebe job variables
/// In zb-client 2.2.0, job.Variables is a JSON string that must be parsed
/// </summary>
public static class JobVariablesHelper
{
    /// <summary>
    /// Parses job variables from JSON string to Dictionary
    /// </summary>
    public static Dictionary<string, object>? ParseVariables(IJob job)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job.Variables))
            {
                return new Dictionary<string, object>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Gets a string value from job variables
    /// </summary>
    public static string? GetString(IJob job, string key, string? defaultValue = null)
    {
        var variables = ParseVariables(job);
        if (variables == null || !variables.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value?.ToString() ?? defaultValue;
    }

    /// <summary>
    /// Gets a Guid value from job variables
    /// </summary>
    public static Guid? GetGuid(IJob job, string key)
    {
        var stringValue = GetString(job, key);
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        return Guid.TryParse(stringValue, out var guid) ? guid : null;
    }

    /// <summary>
    /// Gets a boolean value from job variables
    /// </summary>
    public static bool GetBool(IJob job, string key, bool defaultValue = false)
    {
        var variables = ParseVariables(job);
        if (variables == null || !variables.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value is JsonElement jsonElement2 && jsonElement2.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return bool.TryParse(value?.ToString(), out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Serializes variables to JSON string for job completion
    /// </summary>
    public static string SerializeVariables(object variables)
    {
        return JsonSerializer.Serialize(variables);
    }
}
