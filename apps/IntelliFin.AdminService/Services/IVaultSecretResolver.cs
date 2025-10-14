namespace IntelliFin.AdminService.Services;

public interface IVaultSecretResolver
{
    Task<string> GetAdminConnectionStringAsync(CancellationToken cancellationToken);

    Task<string> GetFinancialConnectionStringAsync(CancellationToken cancellationToken);

    Task<string> GetIdentityConnectionStringAsync(CancellationToken cancellationToken);

    Task<RabbitMqCredentials> GetRabbitMqCredentialsAsync(CancellationToken cancellationToken);

    Task<MinioCredentials> GetMinioCredentialsAsync(CancellationToken cancellationToken);

    Task EnsureSecretsAvailableAsync(CancellationToken cancellationToken);
}

public sealed record RabbitMqCredentials(string UserName, string Password);

public sealed record MinioCredentials(string AccessKey, string SecretKey);
