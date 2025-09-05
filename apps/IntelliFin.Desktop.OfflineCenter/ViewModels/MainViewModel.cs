using IntelliFin.Desktop.OfflineCenter.Services;
using System.Windows.Input;

namespace IntelliFin.Desktop.OfflineCenter.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ISyncService _syncService;
    private readonly IOfflineDataService _offlineDataService;
    
    private bool _isOnline;
    private DateTime? _lastSyncTime;
    private string _connectionStatus = "Checking...";
    private string _currentUser = string.Empty;
    private bool _isAuthenticated;

    public MainViewModel(
        IAuthenticationService authenticationService,
        ISyncService syncService,
        IOfflineDataService offlineDataService)
    {
        _authenticationService = authenticationService;
        _syncService = syncService;
        _offlineDataService = offlineDataService;
        
        Title = "IntelliFin CEO Command Center";
        
        // Commands
        SyncCommand = new Command(async () => await ExecuteAsync(SyncAsync));
        LogoutCommand = new Command(async () => await ExecuteAsync(LogoutAsync));
        
        // Subscribe to events
        _authenticationService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        _syncService.SyncCompleted += OnSyncCompleted;
        
        // Initialize
        _ = Task.Run(InitializeAsync);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public DateTime? LastSyncTime
    {
        get => _lastSyncTime;
        set => SetProperty(ref _lastSyncTime, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public string CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    public ICommand SyncCommand { get; }
    public ICommand LogoutCommand { get; }

    private async Task InitializeAsync()
    {
        await _offlineDataService.InitializeDatabaseAsync();
        
        IsAuthenticated = await _authenticationService.IsAuthenticatedAsync();
        if (IsAuthenticated)
        {
            CurrentUser = await _authenticationService.GetCurrentUserAsync() ?? "Unknown";
        }
        
        LastSyncTime = await _syncService.GetLastSyncTimeAsync();
        await CheckConnectivityAsync();
        
        // Start periodic connectivity checks
        _ = Task.Run(PeriodicConnectivityCheckAsync);
    }

    private async Task SyncAsync()
    {
        if (!IsAuthenticated)
        {
            await Application.Current?.MainPage?.DisplayAlert("Authentication Required", 
                "Please log in to sync data.", "OK");
            return;
        }

        var success = await _syncService.PerformFullSyncAsync();
        if (success)
        {
            LastSyncTime = DateTime.UtcNow;
            await Application.Current?.MainPage?.DisplayAlert("Sync Complete", 
                "Data synchronization completed successfully.", "OK");
        }
        else
        {
            await Application.Current?.MainPage?.DisplayAlert("Sync Failed", 
                "Data synchronization failed. Please check your connection and try again.", "OK");
        }
    }

    private async Task LogoutAsync()
    {
        await _authenticationService.LogoutAsync();
        
        // Navigate to login page or show login dialog
        await Application.Current?.MainPage?.DisplayAlert("Logged Out", 
            "You have been logged out successfully.", "OK");
    }

    private async Task CheckConnectivityAsync()
    {
        try
        {
            IsOnline = Connectivity.NetworkAccess == NetworkAccess.Internet;
            ConnectionStatus = IsOnline ? "Online" : "Offline";
        }
        catch
        {
            IsOnline = false;
            ConnectionStatus = "Connection Error";
        }
    }

    private async Task PeriodicConnectivityCheckAsync()
    {
        while (true)
        {
            await CheckConnectivityAsync();
            await Task.Delay(TimeSpan.FromMinutes(1)); // Check every minute
        }
    }

    private void OnAuthenticationStateChanged(object? sender, AuthenticationStateChangedEventArgs e)
    {
        IsAuthenticated = e.IsAuthenticated;
        CurrentUser = e.Username ?? string.Empty;
    }

    private void OnSyncCompleted(object? sender, SyncCompletedEventArgs e)
    {
        if (e.Success)
        {
            LastSyncTime = DateTime.UtcNow;
        }
    }
}
