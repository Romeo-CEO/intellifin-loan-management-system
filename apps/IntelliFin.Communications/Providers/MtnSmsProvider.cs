using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace IntelliFin.Communications.Providers;

public class MtnSmsProvider : IMtnSmsProvider
{
    private readonly ILogger<MtnSmsProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly SmsProviderSettings _settings;

    public MtnSmsProvider(
        ILogger<MtnSmsProvider> logger,
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
            
            // Validate MTN number
            if (!await ValidateMtnNumberAsync(request.PhoneNumber, cancellationToken))
            {
                return new SmsNotificationResponse
                {
                    NotificationId = notificationId,
                    Status = SmsDeliveryStatus.Failed,
                    ErrorMessage = "Invalid MTN number",
                    UsedProvider = SmsProvider.MTN,
                    SentAt = DateTime.UtcNow
                };
            }

            var payload = new
            {
                apiKey = _settings.ApiKey,
                sender = _settings.SenderId,
                recipient = FormatPhoneNumber(request.PhoneNumber),
                message = request.Message,
                requestDlr = true,
                clientRef = notificationId
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add API key header for MTN
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");

            _logger.LogInformation("Sending SMS via MTN. NotificationId: {NotificationId}", notificationId);

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Parse MTN response
                var mtnResponse = JsonSerializer.Deserialize<MtnSmsResponse>(responseContent);
                
                return new SmsNotificationResponse
                {
                    NotificationId = notificationId,
                    Status = mtnResponse?.StatusCode == "200" ? SmsDeliveryStatus.Sent : SmsDeliveryStatus.Failed,
                    ProviderMessageId = mtnResponse?.MessageId,
                    UsedProvider = SmsProvider.MTN,
                    SentAt = DateTime.UtcNow,
                    Cost = _settings.CostPerSms,
                    ErrorMessage = mtnResponse?.StatusCode != "200" ? mtnResponse?.Description : null
                };
            }
            else
            {
                _logger.LogError("MTN SMS API error. Status: {Status}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new SmsNotificationResponse
                {
                    NotificationId = notificationId,
                    Status = SmsDeliveryStatus.Failed,
                    ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}",
                    UsedProvider = SmsProvider.MTN,
                    SentAt = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS via MTN");
            
            return new SmsNotificationResponse
            {
                NotificationId = Guid.NewGuid().ToString(),
                Status = SmsDeliveryStatus.Failed,
                ErrorMessage = ex.Message,
                UsedProvider = SmsProvider.MTN,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> ValidateMtnNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanNumber = phoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "");
            
            // MTN Zambia prefixes: 95, 96, 97
            if (cleanNumber.StartsWith("260"))
            {
                var prefix = cleanNumber.Substring(3, 2);
                return prefix == "95" || prefix == "96" || prefix == "97";
            }
            
            if (cleanNumber.StartsWith("0"))
            {
                var prefix = cleanNumber.Substring(1, 2);
                return prefix == "95" || prefix == "96" || prefix == "97";
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating MTN number: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return false;
        }
    }

    public async Task<decimal> GetMtnRateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically query MTN API for current rates
            // For now, return configured rate
            return _settings.CostPerSms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MTN rates");
            return _settings.CostPerSms;
        }
    }

    public async Task<string> GetMtnStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically check MTN API health
            var healthCheck = await _httpClient.GetAsync($"{_settings.ApiUrl}/status", cancellationToken);
            return healthCheck.IsSuccessStatusCode ? "Online" : "Offline";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MTN status");
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

    private class MtnSmsResponse
    {
        public string StatusCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public decimal RemainingBalance { get; set; }
    }
}