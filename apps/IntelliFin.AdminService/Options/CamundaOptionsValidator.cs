using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Options;

public sealed class CamundaOptionsValidator : IValidateOptions<CamundaOptions>
{
    public ValidateOptionsResult Validate(string? name, CamundaOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Camunda options are not configured.");
        }

        if (options.FailOpen)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add("Camunda:BaseUrl must be provided when Camunda.FailOpen is false.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenEndpoint))
        {
            failures.Add("Camunda:TokenEndpoint must be provided when Camunda.FailOpen is false.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add("Camunda:ClientId must be provided when Camunda.FailOpen is false.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            failures.Add("Camunda:ClientSecret must be provided when Camunda.FailOpen is false.");
        }

        if (options.TokenRefreshBufferSeconds < 0 || options.TokenRefreshBufferSeconds > 300)
        {
            failures.Add("Camunda:TokenRefreshBufferSeconds must be between 0 and 300 seconds.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
