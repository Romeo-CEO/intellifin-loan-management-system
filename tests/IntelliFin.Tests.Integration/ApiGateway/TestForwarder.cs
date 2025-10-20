using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace IntelliFin.Tests.Integration.ApiGateway;

public sealed class TestForwarder : IHttpForwarder
{
    private readonly object _lock = new();
    private readonly List<ForwardedRequestSnapshot> _requests = new();

    public IReadOnlyList<ForwardedRequestSnapshot> Requests
    {
        get
        {
            lock (_lock)
            {
                return _requests.ToList();
            }
        }
    }

    public ForwardedRequestSnapshot? SingleRequest
    {
        get
        {
            lock (_lock)
            {
                return _requests.LastOrDefault();
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _requests.Clear();
        }
    }

    public Task<ForwarderError> SendAsync(
        HttpContext context,
        string destinationPrefix,
        HttpMessageInvoker httpClient,
        ForwarderRequestConfig? requestConfig)
    {
        var headers = context.Request.Headers
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var snapshot = new ForwardedRequestSnapshot(
            headers,
            context.Request.Path.ToString(),
            headers.TryGetValue("X-Branch-Id", out var branchId) ? branchId : string.Empty,
            headers.TryGetValue("X-Branch-Name", out var branchName) ? branchName : string.Empty,
            headers.TryGetValue("X-Branch-Region", out var branchRegion) ? branchRegion : string.Empty,
            headers.TryGetValue("X-Token-Type", out var tokenType) ? tokenType : string.Empty,
            headers.TryGetValue("X-Correlation-ID", out var correlationId) ? correlationId : string.Empty);

        lock (_lock)
        {
            _requests.Add(snapshot);
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = 0;

        return Task.FromResult(ForwarderError.None);
    }

    public sealed record ForwardedRequestSnapshot(
        IReadOnlyDictionary<string, string> Headers,
        string Path,
        string BranchId,
        string BranchName,
        string BranchRegion,
        string TokenType,
        string CorrelationId);
}
