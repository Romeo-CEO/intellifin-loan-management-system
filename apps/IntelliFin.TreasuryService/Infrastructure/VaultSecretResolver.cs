using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.Commons;
using IntelliFin.TreasuryService.Options;
using System.Threading;

namespace IntelliFin.TreasuryService.Infrastructure;

public interface IVaultSecretResolver
{
    Task<string> GetTreasuryConnectionStringAsync(CancellationToken cancellationToken);
    Task<(string AccessKey, string SecretKey)> GetMinioCredentialsAsync(CancellationToken cancellationToken);
}

public class VaultSecretResolver : IVaultSecretResolver
{
    private readonly IVaultClient _vaultClient;
    private readonly IOptions<VaultOptions> _vaultOptions;

    public VaultSecretResolver(IVaultClient vaultClient, IOptions<VaultOptions> vaultOptions)
    {
        _vaultClient = vaultClient;
        _vaultOptions = vaultOptions;
    }

    public async Task<string> GetTreasuryConnectionStringAsync(CancellationToken cancellationToken)
    {
        try
        {
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                "intellifin/treasury/database");

            return $"Server={secret.Data.Data["server"]};Database={secret.Data.Data["database"]};User Id={secret.Data.Data["username"]};Password={secret.Data.Data["password"]};TrustServerCertificate=true;MultipleActiveResultSets=true";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to retrieve Treasury database connection string from Vault", ex);
        }
    }

    public async Task<(string AccessKey, string SecretKey)> GetMinioCredentialsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                "intellifin/treasury/minio");

            return (
                secret.Data.Data["access_key"].ToString() ?? string.Empty,
                secret.Data.Data["secret_key"].ToString() ?? string.Empty
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to retrieve MinIO credentials from Vault", ex);
        }
    }
}
