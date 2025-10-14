using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.ExceptionHandling;

public sealed class CamundaExceptionHandler(ILogger<CamundaExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not CamundaWorkflowException camundaException)
        {
            return false;
        }

        logger.LogWarning(
            exception,
            "Camunda workflow {WorkflowType} failed with status {StatusCode} (CorrelationId: {CorrelationId})",
            camundaException.WorkflowType,
            (int)camundaException.StatusCode,
            camundaException.CorrelationId);

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Title = "Camunda workflow error",
            Detail = camundaException.Message,
            Type = "https://httpstatuses.com/502",
        };

        problem.Extensions["workflowType"] = camundaException.WorkflowType;
        problem.Extensions["camundaStatus"] = (int)camundaException.StatusCode;
        problem.Extensions["camundaEndpoint"] = camundaException.Endpoint;
        problem.Extensions["correlationId"] = camundaException.CorrelationId ?? string.Empty;
        problem.Extensions["responseBody"] = camundaException.ResponseBody ?? string.Empty;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
