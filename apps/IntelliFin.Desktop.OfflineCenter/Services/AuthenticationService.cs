namespace IntelliFin.Desktop.OfflineCenter.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IFinancialApiService _financialApiService;
    private bool _isAuthenticated;
    private string? _currentUser;

    public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

    public AuthenticationService(IFinancialApiService financialApiService)
    {
        _financialApiService = financialApiService;
        
        // Check if user was previously authenticated
        _currentUser = Preferences.Get("CurrentUser", null);
        _isAuthenticated = !string.IsNullOrEmpty(_currentUser);
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var success = await _financialApiService.AuthenticateAsync(username, password);
            
            if (success)
            {
                _isAuthenticated = true;
                _currentUser = username;
                
                // Store user info
                Preferences.Set("CurrentUser", username);
                Preferences.Set("LastLoginTime", DateTime.UtcNow.ToString());
                
                OnAuthenticationStateChanged(true, username);
            }
            
            return success;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        await _financialApiService.LogoutAsync();
        
        _isAuthenticated = false;
        var previousUser = _currentUser;
        _currentUser = null;
        
        // Clear stored user info
        Preferences.Remove("CurrentUser");
        Preferences.Remove("LastLoginTime");
        
        OnAuthenticationStateChanged(false, null);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        return _isAuthenticated;
    }

    public async Task<string?> GetCurrentUserAsync()
    {
        return _currentUser;
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (!_isAuthenticated || string.IsNullOrEmpty(_currentUser))
        {
            return false;
        }

        try
        {
            // In a real implementation, this would refresh the JWT token
            var isConnected = await _financialApiService.CheckConnectivityAsync();
            return isConnected;
        }
        catch
        {
            return false;
        }
    }

    private void OnAuthenticationStateChanged(bool isAuthenticated, string? username)
    {
        AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
        {
            IsAuthenticated = isAuthenticated,
            Username = username
        });
    }
}
