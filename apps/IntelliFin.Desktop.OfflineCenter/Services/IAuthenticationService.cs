namespace IntelliFin.Desktop.OfflineCenter.Services;

public interface IAuthenticationService
{
    Task<bool> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetCurrentUserAsync();
    Task<bool> RefreshTokenAsync();
    
    event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;
}

public class AuthenticationStateChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
}
