using IntelliFin.IdentityService.Configuration;
using Microsoft.Extensions.Options;

namespace IntelliFin.IdentityService.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityConfiguration _config;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next, 
        IOptions<SecurityConfiguration> config,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _config = config.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_config.EnableSecurityHeaders)
        {
            AddSecurityHeaders(context);
        }

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        try
        {
            var headers = context.Response.Headers;

            if (_config.XContentTypeOptions)
            {
                headers["X-Content-Type-Options"] = "nosniff";
            }

            if (_config.XFrameOptions)
            {
                headers["X-Frame-Options"] = "DENY";
            }

            if (_config.ReferrerPolicy)
            {
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            }

            if (_config.StrictTransportSecurity && context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = $"max-age={_config.HstsMaxAge}; includeSubDomains";
            }

            if (!string.IsNullOrEmpty(_config.ContentSecurityPolicy))
            {
                headers["Content-Security-Policy"] = _config.ContentSecurityPolicy;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding security headers");
        }
    }
}