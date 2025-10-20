using IntelliFin.FinancialService;
using IntelliFin.FinancialService.Clients;
using IntelliFin.FinancialService.Services;
using IntelliFin.Shared.Audit;
using IntelliFin.Tests.Integration.FinancialService.Stubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntelliFin.Tests.Integration.FinancialService;

internal sealed class FinancialServiceWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IAuditClient _auditClient;
    private readonly IAdminAuditClient _adminAuditClient;

    public FinancialServiceWebApplicationFactory(IAuditClient? auditClient = null, IAdminAuditClient? adminAuditClient = null)
    {
        _auditClient = auditClient ?? new TestAuditClient();
        _adminAuditClient = adminAuditClient ?? new TestAdminAuditClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuditClient>();
            services.AddSingleton(_auditClient);

            services.RemoveAll<IAdminAuditClient>();
            services.AddSingleton(_adminAuditClient);

            services.PostConfigure<AuditClientOptions>(options =>
            {
                options.BaseAddress = new Uri("https://adminservice.test");
            });
        });
    }
}
