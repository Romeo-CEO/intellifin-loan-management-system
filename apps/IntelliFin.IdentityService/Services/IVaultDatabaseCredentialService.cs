namespace IntelliFin.IdentityService.Services;

public interface IVaultDatabaseCredentialService
{
    DatabaseCredential GetCurrentCredentials();

    event EventHandler<DatabaseCredential>? CredentialsRotated;
}

public sealed class DatabaseCredential
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string LeaseId { get; set; } = string.Empty;

    public int LeaseDuration { get; set; }
        = 0;

    public bool Renewable { get; set; }
        = true;

    public DateTime LoadedAt { get; set; }
        = DateTime.UtcNow;
}
