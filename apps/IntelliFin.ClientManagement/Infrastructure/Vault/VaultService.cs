using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace IntelliFin.ClientManagement.Infrastructure.Vault;

/// <summary>
/// Service for retrieving secrets from HashiCorp Vault
/// </summary>
public interface IVaultService
{
    /// <summary>
    /// Retrieves connection string from Vault
    /// </summary>
    /// <param name="path">Vault secret path</param>
    /// <returns>Connection string</returns>
    Task<string> GetConnectionStringAsync(string path);
}

public class VaultService : IVaultService
{
    private readonly IVaultClient _vaultClient;
    private readonly ILogger<VaultService> _logger;

    public VaultService(IConfiguration configuration, ILogger<VaultService> logger)
    {
        _logger = logger;
        
        var vaultEndpoint = configuration["Vault:Endpoint"] ?? "http://vault:8200";
        var vaultToken = configuration["Vault:Token"] ?? Environment.GetEnvironmentVariable("VAULT_TOKEN") ?? "";
        
        _logger.LogInformation("Initializing Vault client with endpoint: {Endpoint}", vaultEndpoint);
        
        var authMethod = new TokenAuthMethodInfo(vaultToken);
        var vaultClientSettings = new VaultClientSettings(vaultEndpoint, authMethod);
        _vaultClient = new VaultClient(vaultClientSettings);
    }

    public async Task<string> GetConnectionStringAsync(string path)
    {
        try
        {
            _logger.LogInformation("Retrieving connection string from Vault path: {Path}", path);
            
            Secret<SecretData> secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                path: path,
                mountPoint: "secret"
            );
            
            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue("connectionString", out var connectionString))
            {
                _logger.LogInformation("Successfully retrieved connection string from Vault");
                return connectionString?.ToString() ?? throw new InvalidOperationException("Connection string is null");
            }
            
            throw new InvalidOperationException($"Connection string not found at Vault path: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve connection string from Vault at path: {Path}. Falling back to appsettings.json for development.", path);
            throw;
        }
    }
}
