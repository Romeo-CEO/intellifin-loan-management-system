using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IntelliFin.AdminService.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresMfaAttribute : Attribute, IAsyncAuthorizationFilter
{
    public int TimeoutMinutes { get; set; } = 15;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated is not true)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var amrValues = ExtractAmrValues(user);
        if (!amrValues.Contains("mfa", StringComparer.OrdinalIgnoreCase))
        {
            var operation = context.ActionDescriptor.DisplayName ?? "unknown";
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "mfa_required",
                message = "This operation requires multi-factor authentication.",
                mfaChallengeUrl = "/api/admin/mfa/challenge",
                operation,
                timeoutMinutes = TimeoutMinutes
            });

            return Task.CompletedTask;
        }

        if (!IsMfaStillValid(user, TimeoutMinutes))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "mfa_expired",
                message = "MFA validation has expired. Please re-authenticate.",
                mfaChallengeUrl = "/api/admin/mfa/challenge"
            });

            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyCollection<string> ExtractAmrValues(ClaimsPrincipal user)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in user.FindAll("amr"))
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            if (claim.Value.Contains('[', StringComparison.Ordinal) && claim.Value.Contains(']', StringComparison.Ordinal))
            {
                try
                {
                    var values = JsonSerializer.Deserialize<string[]>(claim.Value);
                    if (values is not null)
                    {
                        foreach (var value in values)
                        {
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                result.Add(value.Trim());
                            }
                        }
                    }

                    continue;
                }
                catch (JsonException)
                {
                    // Fallback to treating as delimited string
                }
            }

            foreach (var value in claim.Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static bool IsMfaStillValid(ClaimsPrincipal user, int timeoutMinutes)
    {
        var iatClaim = user.FindFirst("mfa_iat") ?? user.FindFirst("iat");
        if (iatClaim is null)
        {
            return true;
        }

        if (!long.TryParse(iatClaim.Value, out var unixSeconds))
        {
            return true;
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return DateTimeOffset.UtcNow <= issuedAt.AddMinutes(timeoutMinutes);
    }
}
