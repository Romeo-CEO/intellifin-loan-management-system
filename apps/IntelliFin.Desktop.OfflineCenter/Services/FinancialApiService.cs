using IntelliFin.Desktop.OfflineCenter.Models;
using Microsoft.Maui.Storage;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace IntelliFin.Desktop.OfflineCenter.Services;

public class FinancialApiService : IFinancialApiService
{
    private const string RefreshTokenStorageKey = "IntelliFin.RefreshToken";
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _authToken;
    private string? _refreshToken;

    public FinancialApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000") // API Gateway URL
        };
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<bool> CheckConnectivityAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        try
        {
            var loginRequest = new { Username = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest, _jsonOptions);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
                _authToken = result?.AccessToken;
                _refreshToken = result?.RefreshToken;

                if (!string.IsNullOrEmpty(_authToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _authToken);
                }

                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    await PersistRefreshTokenAsync(_refreshToken);
                }

                return !string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_refreshToken);
            }

            await ClearTokensAsync();
            return false;
        }
        catch
        {
            await ClearTokensAsync();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        await ClearTokensAsync();
    }

    public async Task<bool> RefreshSessionAsync(string? deviceId = null)
    {
        try
        {
            var refreshToken = await GetStoredRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            var refreshRequest = new RefreshRequest
            {
                RefreshToken = refreshToken,
                DeviceId = deviceId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", refreshRequest, _jsonOptions);
            if (!response.IsSuccessStatusCode)
            {
                await ClearTokensAsync();
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<RefreshResponse>(_jsonOptions);
            if (result == null || string.IsNullOrEmpty(result.AccessToken) || string.IsNullOrEmpty(result.RefreshToken))
            {
                await ClearTokensAsync();
                return false;
            }

            _authToken = result.AccessToken;
            _refreshToken = result.RefreshToken;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authToken);

            await PersistRefreshTokenAsync(_refreshToken);

            return true;
        }
        catch
        {
            await ClearTokensAsync();
            return false;
        }
    }

    public async Task<IEnumerable<OfflineLoan>> FetchLoansAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/financial/loans");
            if (response.IsSuccessStatusCode)
            {
                var loans = await response.Content.ReadFromJsonAsync<List<OfflineLoan>>(_jsonOptions);
                return loans ?? new List<OfflineLoan>();
            }
        }
        catch
        {
            // Log error
        }
        return new List<OfflineLoan>();
    }

    public async Task<IEnumerable<OfflineClient>> FetchClientsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/clients");
            if (response.IsSuccessStatusCode)
            {
                var clients = await response.Content.ReadFromJsonAsync<List<OfflineClient>>(_jsonOptions);
                return clients ?? new List<OfflineClient>();
            }
        }
        catch
        {
            // Log error
        }
        return new List<OfflineClient>();
    }

    public async Task<IEnumerable<OfflinePayment>> FetchPaymentsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/financial/payments");
            if (response.IsSuccessStatusCode)
            {
                var payments = await response.Content.ReadFromJsonAsync<List<OfflinePayment>>(_jsonOptions);
                return payments ?? new List<OfflinePayment>();
            }
        }
        catch
        {
            // Log error
        }
        return new List<OfflinePayment>();
    }

    public async Task<OfflineFinancialSummary> FetchFinancialSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/financial/summary");
            if (response.IsSuccessStatusCode)
            {
                var summary = await response.Content.ReadFromJsonAsync<OfflineFinancialSummary>(_jsonOptions);
                return summary ?? new OfflineFinancialSummary();
            }
        }
        catch
        {
            // Log error
        }
        return new OfflineFinancialSummary();
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/financial/dashboard");
            if (response.IsSuccessStatusCode)
            {
                var summary = await response.Content.ReadFromJsonAsync<DashboardSummary>(_jsonOptions);
                return summary ?? new DashboardSummary();
            }
        }
        catch
        {
            // Log error
        }
        return new DashboardSummary();
    }

    public async Task<decimal> GetAccountBalanceAsync(int accountId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/gl/accounts/{accountId}/balance");
            if (response.IsSuccessStatusCode)
            {
                var balance = await response.Content.ReadFromJsonAsync<decimal>(_jsonOptions);
                return balance;
            }
        }
        catch
        {
            // Log error
        }
        return 0;
    }

    public async Task<IEnumerable<LoanSummary>> GetLoanSummariesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/financial/loans/summaries");
            if (response.IsSuccessStatusCode)
            {
                var summaries = await response.Content.ReadFromJsonAsync<List<LoanSummary>>(_jsonOptions);
                return summaries ?? new List<LoanSummary>();
            }
        }
        catch
        {
            // Log error
        }
        return new List<LoanSummary>();
    }

    public async Task<string> GenerateTrialBalanceReportAsync(DateTime asOfDate)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/gl/reports/trial-balance?asOfDate={asOfDate:yyyy-MM-dd}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch
        {
            // Log error
        }
        return string.Empty;
    }

    public async Task<string> GenerateBoZReportAsync(DateTime reportDate)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/gl/reports/boz?reportDate={reportDate:yyyy-MM-dd}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch
        {
            // Log error
        }
        return string.Empty;
    }

    public async Task<string> GenerateCollectionsReportAsync(DateTime reportDate)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/collections/reports?reportDate={reportDate:yyyy-MM-dd}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch
        {
            // Log error
        }
        return string.Empty;
    }

    public async Task<bool> ProcessPaymentAsync(string loanId, decimal amount, string paymentMethod)
    {
        try
        {
            var request = new { LoanId = loanId, Amount = amount, PaymentMethod = paymentMethod };
            var response = await _httpClient.PostAsJsonAsync("/api/payments/process", request, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateLoanStatusAsync(string loanId, string status)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/collections/accounts/{loanId}/status", status, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckFinancialServiceHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/financial/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckPmecConnectivityAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/pmec/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckPaymentGatewayHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/payments/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task PersistRefreshTokenAsync(string refreshToken)
    {
        try
        {
            await SecureStorage.Default.SetAsync(RefreshTokenStorageKey, refreshToken);
        }
        catch
        {
            Preferences.Set(RefreshTokenStorageKey, refreshToken);
        }
    }

    private async Task<string?> GetStoredRefreshTokenAsync()
    {
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            return _refreshToken;
        }

        try
        {
            var stored = await SecureStorage.Default.GetAsync(RefreshTokenStorageKey);
            if (!string.IsNullOrEmpty(stored))
            {
                _refreshToken = stored;
                return stored;
            }
        }
        catch
        {
            var stored = Preferences.Get(RefreshTokenStorageKey, null);
            _refreshToken = stored;
            return stored;
        }

        return null;
    }

    private Task ClearTokensAsync()
    {
        _authToken = null;
        _refreshToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;

        try
        {
            SecureStorage.Default.Remove(RefreshTokenStorageKey);
        }
        catch
        {
            Preferences.Remove(RefreshTokenStorageKey);
        }

        return Task.CompletedTask;
    }
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public string RefreshTokenFamilyId { get; set; } = string.Empty;
}

public class RefreshResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public string RefreshTokenFamilyId { get; set; } = string.Empty;
}

internal class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
}
