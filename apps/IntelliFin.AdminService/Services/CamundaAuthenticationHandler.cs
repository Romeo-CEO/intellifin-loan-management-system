using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class CamundaAuthenticationHandler : DelegatingHandler
{
    private readonly ICamundaTokenProvider _tokenProvider;
    private readonly IOptionsMonitor<CamundaOptions> _optionsMonitor;
    private readonly ILogger<CamundaAuthenticationHandler> _logger;

    public CamundaAuthenticationHandler(
        ICamundaTokenProvider tokenProvider,
        IOptionsMonitor<CamundaOptions> optionsMonitor,
        ILogger<CamundaAuthenticationHandler> logger)
    {
        _tokenProvider = tokenProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (options.FailOpen && string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _logger.LogWarning("Camunda FailOpen is enabled; skipping bearer token attachment for request {Method} {RequestUri}", request.Method, request.RequestUri);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
