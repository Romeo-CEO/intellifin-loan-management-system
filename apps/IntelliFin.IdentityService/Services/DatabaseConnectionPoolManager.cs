using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace IntelliFin.IdentityService.Services;

public interface IDatabaseConnectionPoolManager : IDisposable
{
    Task DrainOldConnectionsAsync(string oldUsername, CancellationToken cancellationToken);
}

public sealed class DatabaseConnectionPoolManager : IDatabaseConnectionPoolManager
{
    private readonly IVaultDatabaseCredentialService _credentialService;
    private readonly ILogger<DatabaseConnectionPoolManager> _logger;

    public DatabaseConnectionPoolManager(
        IVaultDatabaseCredentialService credentialService,
        ILogger<DatabaseConnectionPoolManager> logger)
    {
        _credentialService = credentialService;
        _logger = logger;
        _credentialService.CredentialsRotated += HandleCredentialsRotated;
    }

    private async void HandleCredentialsRotated(object? sender, DatabaseCredential newCredential)
    {
        try
        {
            _logger.LogInformation("Vault credentials rotated. Clearing SQL connection pools for user {Username}", newCredential.Username);
            SqlConnection.ClearAllPools();
            await Task.Delay(TimeSpan.FromMinutes(5));
            _logger.LogInformation("Connection pool drain completed after credential rotation for {Username}", newCredential.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error draining SQL connection pools after Vault credential rotation");
        }
    }

    public async Task DrainOldConnectionsAsync(string oldUsername, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manually draining SQL connection pools for user {Username}", oldUsername);
        SqlConnection.ClearAllPools();
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
    }

    public void Dispose()
    {
        _credentialService.CredentialsRotated -= HandleCredentialsRotated;
    }
}
