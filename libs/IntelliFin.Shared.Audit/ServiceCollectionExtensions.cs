using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliFin.Shared.Audit;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuditClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuditClientOptions>(options =>
            configuration.GetSection(AuditClientOptions.SectionName).Bind(options));

        services.AddHttpClient<IAuditClient, AuditClient>();
        return services;
    }
}
