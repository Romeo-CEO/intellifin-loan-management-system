using System;
using System.Collections.Generic;
using System.Security.Claims;
using IntelliFin.AdminService;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace IntelliFin.Tests.Integration.AdminService;

public sealed class AdminServiceWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestCamundaHandler CamundaHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                [$"{CamundaOptions.SectionName}:BaseUrl"] = "https://camunda.test/engine-rest/",
                [$"{CamundaOptions.SectionName}:TokenEndpoint"] = "https://keycloak.test/realms/intellifin/protocol/openid-connect/token",
                [$"{CamundaOptions.SectionName}:ClientId"] = "admin-service",
                [$"{CamundaOptions.SectionName}:ClientSecret"] = "test-secret",
                [$"{CamundaOptions.SectionName}:FailOpen"] = "false",
                [$"{CamundaOptions.SectionName}:TokenRefreshBufferSeconds"] = "30",
                [$"{IncidentResponseOptions.SectionName}:IncidentWorkflowProcessId"] = "incident-flow",
                [$"{IncidentResponseOptions.SectionName}:PostmortemWorkflowProcessId"] = "postmortem-flow",
                ["Vault:Enabled"] = "false"
            };

            configBuilder.AddInMemoryCollection(overrides!);
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new TestUserStartupFilter());
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVaultSecretResolver>();
            services.AddSingleton<IVaultSecretResolver>(new TestVaultSecretResolver());

            services.RemoveAll<ICamundaTokenProvider>();
            services.AddSingleton<ICamundaTokenProvider>(new StubCamundaTokenProvider("test-token"));

            services.AddSingleton(CamundaHandler);
            services.AddSingleton<IHttpMessageHandlerBuilderFilter>(sp => new TestCamundaHandlerFilter(sp.GetRequiredService<TestCamundaHandler>()));

            services.RemoveAll<DbContextOptions<AdminDbContext>>();
            services.RemoveAll<AdminDbContext>();
            services.AddDbContext<AdminDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.RemoveAll<DbContextOptions<FinancialDbContext>>();
            services.RemoveAll<FinancialDbContext>();
            services.AddDbContext<FinancialDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.RemoveAll<DbContextOptions<LmsDbContext>>();
            services.RemoveAll<LmsDbContext>();
            services.AddDbContext<LmsDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        });
    }

    private sealed class TestUserStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    var identity = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "test-user"),
                        new Claim(ClaimTypes.Name, "Test User")
                    }, "Test");

                    context.User = new ClaimsPrincipal(identity);
                    await nextMiddleware();
                });

                next(app);
            };
        }
    }
}
