using System.Net;

namespace IntelliFin.FinancialService.Exceptions;

public sealed class AuditForwardingException : Exception
{
    public AuditForwardingException(string message)
        : base(message)
    {
    }

    public AuditForwardingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AuditForwardingException(string message, HttpStatusCode statusCode, string? responseBody)
        : base($"{message} StatusCode={(int)statusCode}. Response={responseBody ?? "<empty>"}.")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseBody { get; }
}
