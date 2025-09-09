using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Middleware;

namespace IntelliFin.IdentityService.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigureIdentityApplication(this WebApplication app)
    {
        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        // Security headers
        app.UseSecurityHeaders();

        // CORS
        app.UseCors("IntelliFin");

        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Controllers
        app.MapControllers();

        // Health checks
        app.MapHealthChecks("/health");

        // Root endpoint
        app.MapGet("/", () => Results.Ok(new { 
            name = "IntelliFin.IdentityService", 
            status = "OK",
            version = "1.0.0",
            timestamp = DateTime.UtcNow 
        }));

        return app;
    }

    private static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // Security headers
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            
            if (context.Request.IsHttps)
            {
                context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            }

            await next();
        });

        return app;
    }
}