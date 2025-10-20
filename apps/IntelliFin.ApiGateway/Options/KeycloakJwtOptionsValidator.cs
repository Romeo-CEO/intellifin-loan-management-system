using System;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IntelliFin.ApiGateway.Options;

public sealed class KeycloakJwtOptionsValidator : IValidateOptions<KeycloakJwtOptions>
{
    private readonly IHostEnvironment _environment;

    public KeycloakJwtOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, KeycloakJwtOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Keycloak JWT options were not provided.");
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            failures.Add("Authentication:KeycloakJwt:Authority must be configured.");
        }
        else if (!IsHttpsUrl(options.Authority))
        {
            failures.Add("Authentication:KeycloakJwt:Authority must use https://.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Authentication:KeycloakJwt:Issuer must be configured.");
        }
        else if (!IsHttpsUrl(options.Issuer))
        {
            failures.Add("Authentication:KeycloakJwt:Issuer must use https://.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Authentication:KeycloakJwt:Audience must be configured.");
        }

        if (!string.IsNullOrWhiteSpace(options.MetadataAddress) && !IsHttpsUrl(options.MetadataAddress))
        {
            failures.Add("Authentication:KeycloakJwt:MetadataAddress must use https:// when specified.");
        }

        if (!_environment.IsDevelopment() && !options.RequireHttps)
        {
            failures.Add("Authentication:KeycloakJwt:RequireHttps must be true outside of Development.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsHttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
