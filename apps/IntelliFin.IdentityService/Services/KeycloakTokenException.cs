using System.Net;

namespace IntelliFin.IdentityService.Services;

public class KeycloakTokenException : Exception
{
    public KeycloakTokenException(HttpStatusCode statusCode, string? message = null, Exception? innerException = null)
        : base(message ?? $"Keycloak token endpoint returned status code {(int)statusCode} ({statusCode}).", innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
