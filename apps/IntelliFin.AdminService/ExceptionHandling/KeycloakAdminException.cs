using System.Net;

namespace IntelliFin.AdminService.ExceptionHandling;

public sealed class KeycloakAdminException(HttpStatusCode statusCode, string message, string? error, string? errorDescription)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? Error { get; } = error;
    public string? ErrorDescription { get; } = errorDescription;
}
