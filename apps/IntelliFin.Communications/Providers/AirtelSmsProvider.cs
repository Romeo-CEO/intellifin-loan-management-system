using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace IntelliFin.Communications.Providers;

public class AirtelSmsProvider : IAirtelSmsProvider
{
    private readonly ILogger<AirtelSmsProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly SmsProviderSettings _settings;

    public AirtelSmsProvider(
        ILogger<AirtelSmsProvider> logger,
        HttpClient httpClient,
        IOptions<SmsProviderSettings> settings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<SmsNotificationResponse> SendSmsAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var notificationId = Guid.NewGuid().ToString();
            
            // Validate Airtel number
            if (!await ValidateAirtelNumberAsync(request.PhoneNumber, cancellationToken))
            {
                return new SmsNotificationResponse
                {
                    NotificationId = notificationId,
                    Status = SmsDeliveryStatus.Failed,
                    ErrorMessage = "Invalid Airtel number",
                    UsedProvider = SmsProvider.Airtel,
                    SentAt = DateTime.UtcNow
                };
            }

            var payload = new
            {
                username = _settings.Username,
                password = _settings.Password,
                source = _settings.SenderId,
                destination = FormatPhoneNumber(request.PhoneNumber),
                message = request.Message,
                dlr = "1", // Request delivery report
                reference = notificationId
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending SMS via Airtel. NotificationId: {NotificationId}", notificationId);

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Parse Airtel response
                var airtelResponse = JsonSerializer.Deserialize<AirtelSmsResponse>(responseContent);
                
                return new SmsNotificationResponse
                {
                    NotificationId = notificationId,
                    Status = airtelResponse?.Status == "Success" ? SmsDeliveryStatus.Sent : SmsDeliveryStatus.Failed,
                    ProviderMessageId = airtelResponse?.MessageId,
                    UsedProvider = SmsProvider.Airtel,
                    SentAt = DateTime.UtcNow,
                    Cost = _settings.CostPerSms,
                    ErrorMessage = airtelResponse?.Status != "Success" ? airtelResponse?.Message : null
                };
            }
            else
            {
                _logger.LogError("Airtel SMS API error. Status: {Status}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new SmsNotificationResponse
                {
                    NotificationId = notificationId,
                    Status = SmsDeliveryStatus.Failed,
                    ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}",
                    UsedProvider = SmsProvider.Airtel,
                    SentAt = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS via Airtel");
            
            return new SmsNotificationResponse
            {
                NotificationId = Guid.NewGuid().ToString(),
                Status = SmsDeliveryStatus.Failed,
                ErrorMessage = ex.Message,
                UsedProvider = SmsProvider.Airtel,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> ValidateAirtelNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanNumber = phoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "");
            
            // Airtel Zambia prefixes: 76, 77
            if (cleanNumber.StartsWith("260"))
            {
                var prefix = cleanNumber.Substring(3, 2);
                return prefix == "76" || prefix == "77";
            }
            
            if (cleanNumber.StartsWith("0"))
            {
                var prefix = cleanNumber.Substring(1, 2);
                return prefix == "76" || prefix == "77";
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Airtel number: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return false;
        }
    }

    public async Task<decimal> GetAirtelRateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically query Airtel API for current rates
            // For now, return configured rate
            return _settings.CostPerSms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Airtel rates");
            return _settings.CostPerSms;
        }
    }

    public async Task<string> GetAirtelStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically check Airtel API health
            var healthCheck = await _httpClient.GetAsync($"{_settings.ApiUrl}/health", cancellationToken);
            return healthCheck.IsSuccessStatusCode ? "Online" : "Offline";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Airtel status");
            return "Unknown";
        }
    }

    private string FormatPhoneNumber(string phoneNumber)
    {
        var cleanNumber = phoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "");
        
        if (cleanNumber.StartsWith("0"))
        {
            return "260" + cleanNumber[1..];
        }
        
        if (!cleanNumber.StartsWith("260"))
        {
            return "260" + cleanNumber;
        }
        
        return cleanNumber;
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
            return "****";
        
        return phoneNumber[..2] + "****" + phoneNumber[^2..];
    }

    private class AirtelSmsResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}