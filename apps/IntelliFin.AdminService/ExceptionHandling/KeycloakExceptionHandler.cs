using IntelliFin.AdminService.ExceptionHandling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.ExceptionHandling;

public sealed class KeycloakExceptionHandler(ILogger<KeycloakExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not KeycloakAdminException keycloakException)
        {
            return false;
        }

        logger.LogWarning(
            exception,
            "Keycloak admin API request failed with status {StatusCode}",
            (int)keycloakException.StatusCode);

        httpContext.Response.StatusCode = (int)keycloakException.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)keycloakException.StatusCode,
            Title = "Keycloak admin API error",
            Detail = keycloakException.Message,
            Type = "https://httpstatuses.com/" + (int)keycloakException.StatusCode,
            Extensions =
            {
                ["error"] = keycloakException.Error ?? string.Empty,
                ["errorDescription"] = keycloakException.ErrorDescription ?? string.Empty
            }
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
