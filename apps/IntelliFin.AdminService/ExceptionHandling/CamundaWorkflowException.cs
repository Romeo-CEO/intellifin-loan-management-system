using System;
using System.Net;

namespace IntelliFin.AdminService.ExceptionHandling;

public sealed class CamundaWorkflowException : Exception
{
    public CamundaWorkflowException(HttpStatusCode statusCode, string endpoint, string workflowType, string? responseBody, string? correlationId)
        : base($"Camunda workflow call to '{endpoint}' failed with status {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
        WorkflowType = workflowType;
        ResponseBody = responseBody;
        CorrelationId = correlationId;
    }

    public HttpStatusCode StatusCode { get; }

    public string Endpoint { get; }

    public string WorkflowType { get; }

    public string? ResponseBody { get; }

    public string? CorrelationId { get; }
}
