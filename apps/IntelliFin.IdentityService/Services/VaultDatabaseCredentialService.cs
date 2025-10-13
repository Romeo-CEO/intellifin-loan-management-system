using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IntelliFin.IdentityService.Configuration;

namespace IntelliFin.IdentityService.Services;

public class VaultDatabaseCredentialService : BackgroundService, IVaultDatabaseCredentialService
{
    private readonly ILogger<VaultDatabaseCredentialService> _logger;
    private readonly VaultConfiguration _options;
    private readonly SemaphoreSlim _credentialLock = new(1, 1);
    private readonly string _credentialsPath;
    private FileSystemWatcher? _fileWatcher;
    private DatabaseCredential? _currentCredentials;

    public VaultDatabaseCredentialService(
        IOptions<VaultConfiguration> options,
        IConfiguration configuration,
        ILogger<VaultDatabaseCredentialService> logger)
    {
        _logger = logger;
        _options = options.Value;
        _credentialsPath = !string.IsNullOrWhiteSpace(configuration["DATABASE_CREDENTIALS_PATH"])
            ? configuration["DATABASE_CREDENTIALS_PATH"]!
            : _options.CredentialsPath;
    }

    public event EventHandler<DatabaseCredential>? CredentialsRotated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Vault credential watcher at {Path}", _credentialsPath);

        await EnsureCredentialsLoadedAsync(stoppingToken);
        SetupFileWatcher();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public DatabaseCredential GetCurrentCredentials()
    {
        if (_currentCredentials is not null)
        {
            return _currentCredentials;
        }

        LoadCredentialsAsync(CancellationToken.None).GetAwaiter().GetResult();

        return _currentCredentials ?? throw new InvalidOperationException(
            "Vault database credentials are not yet available. Ensure the Vault Agent sidecar is running.");
    }

    private async Task EnsureCredentialsLoadedAsync(CancellationToken cancellationToken)
    {
        if (_currentCredentials is null)
        {
            await LoadCredentialsAsync(cancellationToken);
        }
    }

    private async Task LoadCredentialsAsync(CancellationToken cancellationToken)
    {
        await _credentialLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                _logger.LogWarning("Vault credentials file not found at {Path}. Waiting for Vault Agent...", _credentialsPath);
                return;
            }

            await using var stream = File.OpenRead(_credentialsPath);
            var credentials = await JsonSerializer.DeserializeAsync<DatabaseCredential>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            if (credentials is null)
            {
                _logger.LogError("Failed to deserialize Vault credentials from {Path}", _credentialsPath);
                return;
            }

            credentials.LoadedAt = DateTime.UtcNow;

            var previous = _currentCredentials;
            _currentCredentials = credentials;

            _logger.LogInformation(
                "Vault credentials loaded. Username={Username} Lease={LeaseId} Renewable={Renewable}",
                credentials.Username,
                credentials.LeaseId,
                credentials.Renewable);

            if (previous is not null && previous.Username != credentials.Username)
            {
                CredentialsRotated?.Invoke(this, credentials);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Vault credentials from {Path}", _credentialsPath);
        }
        finally
        {
            _credentialLock.Release();
        }
    }

    private void SetupFileWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(_credentialsPath);
            var fileName = Path.GetFileName(_credentialsPath);

            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("Invalid Vault credential path {Path}; file watcher not configured", _credentialsPath);
                return;
            }

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _fileWatcher.Changed += async (_, _) =>
            {
                // Debounce rapid writes from Vault Agent
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                await LoadCredentialsAsync(CancellationToken.None);
            };

            _fileWatcher.EnableRaisingEvents = true;
            _logger.LogInformation("Watching Vault credentials for changes at {Path}", _credentialsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start file watcher for Vault credentials at {Path}", _credentialsPath);
        }
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        _credentialLock.Dispose();
        base.Dispose();
    }
}
