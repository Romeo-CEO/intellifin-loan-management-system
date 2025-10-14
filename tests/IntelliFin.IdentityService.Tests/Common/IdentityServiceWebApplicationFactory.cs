using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace IntelliFin.IdentityService.Tests.Common;

public class IdentityServiceWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Features:DisableSqlHealthCheck"] = "true",
                ["ConnectionStrings:IdentityDb"] = "Server=localhost;Database=IntellifinIdentity;Trusted_Connection=True;TrustServerCertificate=True;"
            };

            configurationBuilder.AddInMemoryCollection(overrides!);
        });
    }
}
