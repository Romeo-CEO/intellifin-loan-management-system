using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Services;

namespace IntelliFin.Tests.Integration.AdminService;

public sealed class TestVaultSecretResolver : IVaultSecretResolver
{
    public Task<string> GetAdminConnectionStringAsync(CancellationToken cancellationToken)
        => Task.FromResult("Server=(localdb)\\MSSQLLocalDB;Database=IntelliFin_Admin_Test;Trusted_Connection=True;MultipleActiveResultSets=true");

    public Task<string> GetFinancialConnectionStringAsync(CancellationToken cancellationToken)
        => Task.FromResult("Server=(localdb)\\MSSQLLocalDB;Database=IntelliFin_Financial_Test;Trusted_Connection=True;MultipleActiveResultSets=true");

    public Task<string> GetIdentityConnectionStringAsync(CancellationToken cancellationToken)
        => Task.FromResult("Server=(localdb)\\MSSQLLocalDB;Database=IntelliFin_Identity_Test;Trusted_Connection=True;MultipleActiveResultSets=true");

    public Task<RabbitMqCredentials> GetRabbitMqCredentialsAsync(CancellationToken cancellationToken)
        => Task.FromResult(new RabbitMqCredentials("test", "test"));

    public Task<MinioCredentials> GetMinioCredentialsAsync(CancellationToken cancellationToken)
        => Task.FromResult(new MinioCredentials("test", "test"));

    public Task EnsureSecretsAvailableAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
