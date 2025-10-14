using IntelliFin.IdentityService.Middleware;

namespace IntelliFin.IdentityService.Extensions;

public static class WebApplicationExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        return app;
    }
}
