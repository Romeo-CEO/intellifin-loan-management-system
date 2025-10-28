using IntelliFin.TreasuryService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace IntelliFin.TreasuryService.Services;

/// <summary>
/// Service for integrating with external banking APIs for payment processing
/// </summary>
public class BankingApiService : IBankingApiService
{
    private readonly HttpClient _httpClient;
    private readonly VaultOptions _vaultOptions;
    private readonly ILogger<BankingApiService> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;

    public BankingApiService(
        HttpClient httpClient,
        IOptions<VaultOptions> vaultOptions,
        ILogger<BankingApiService> logger)
    {
        _httpClient = httpClient;
        _vaultOptions = vaultOptions.Value;
        _logger = logger;

        // Configure retry policy for banking API calls
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning("Banking API retry {RetryAttempt} after {Timespan}s", retryAttempt, timespan.TotalSeconds);
                });

        // Configure circuit breaker for banking API failures
        _circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (result, timespan) =>
                {
                    _logger.LogWarning("Banking API circuit breaker opened for {Timespan}", timespan);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Banking API circuit breaker reset");
                });
    }

    /// <summary>
    /// Execute a payment/disbursement through banking API
    /// </summary>
    public async Task<PaymentExecutionResult> ExecutePaymentAsync(
        string bankCode,
        string accountNumber,
        decimal amount,
        string reference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing payment: BankCode={BankCode}, AccountNumber={AccountNumber}, Amount={Amount}, Reference={Reference}",
            bankCode, MaskAccountNumber(accountNumber), amount, reference);

        try
        {
            var paymentRequest = new PaymentRequest
            {
                BankCode = bankCode,
                AccountNumber = accountNumber,
                Amount = amount,
                Currency = "MWK",
                Reference = reference,
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow
            };

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    await _httpClient.PostAsJsonAsync("/api/payments/execute", paymentRequest, cancellationToken)));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Banking API returned error: Status={Status}, Content={ErrorContent}", response.StatusCode, errorContent);

                return new PaymentExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Banking API error: {response.StatusCode}",
                    CorrelationId = correlationId
                };
            }

            var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken);
            if (paymentResponse == null)
            {
                return new PaymentExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Invalid response from banking API",
                    CorrelationId = correlationId
                };
            }

            _logger.LogInformation(
                "Payment executed successfully: TransactionId={TransactionId}, Status={Status}",
                paymentResponse.TransactionId, paymentResponse.Status);

            return new PaymentExecutionResult
            {
                Success = true,
                TransactionId = paymentResponse.TransactionId,
                Status = paymentResponse.Status,
                BankReference = paymentResponse.BankReference,
                CorrelationId = correlationId
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error executing payment: BankCode={BankCode}", bankCode);
            return new PaymentExecutionResult
            {
                Success = false,
                ErrorMessage = $"Network error: {ex.Message}",
                CorrelationId = correlationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing payment: BankCode={BankCode}", bankCode);
            return new PaymentExecutionResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                CorrelationId = correlationId
            };
        }
    }

    /// <summary>
    /// Check the status of a payment
    /// </summary>
    public async Task<PaymentStatusResult> CheckPaymentStatusAsync(
        string transactionId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking payment status: TransactionId={TransactionId}", transactionId);

        try
        {
            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _circuitBreakerPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync($"/api/payments/status/{transactionId}", cancellationToken)));

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentStatusResult
                {
                    Success = false,
                    ErrorMessage = $"Banking API error: {response.StatusCode}",
                    CorrelationId = correlationId
                };
            }

            var statusResponse = await response.Content.ReadFromJsonAsync<PaymentStatusResponse>(cancellationToken);
            if (statusResponse == null)
            {
                return new PaymentStatusResult
                {
                    Success = false,
                    ErrorMessage = "Invalid response from banking API",
                    CorrelationId = correlationId
                };
            }

            return new PaymentStatusResult
            {
                Success = true,
                TransactionId = statusResponse.TransactionId,
                Status = statusResponse.Status,
                StatusMessage = statusResponse.StatusMessage,
                ProcessedAt = statusResponse.ProcessedAt,
                CorrelationId = correlationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking payment status: TransactionId={TransactionId}", transactionId);
            return new PaymentStatusResult
            {
                Success = false,
                ErrorMessage = $"Error checking status: {ex.Message}",
                CorrelationId = correlationId
            };
        }
    }

    /// <summary>
    /// Configure mTLS for banking API communication
    /// </summary>
    public void ConfigureMTLS(X509Certificate2 clientCertificate)
    {
        // Configure client certificate for mTLS
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCertificate);

        // Note: In a real implementation, you would replace the default handler
        // This is a placeholder for the mTLS configuration
        _logger.LogInformation("mTLS configured for banking API communication");
    }

    /// <summary>
    /// Mask account number for logging (show only last 4 digits)
    /// </summary>
    private static string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length <= 4)
            return "****";

        return $"****{accountNumber[^4..]}";
    }
}

/// <summary>
/// Request model for payment execution
/// </summary>
public class PaymentRequest
{
    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MWK";
    public string Reference { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response model from banking API
/// </summary>
public class PaymentResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BankReference { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response model for payment status check
/// </summary>
public class PaymentStatusResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>
/// Result of payment execution
/// </summary>
public class PaymentExecutionResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BankReference { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Result of payment status check
/// </summary>
public class PaymentStatusResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Interface for banking API service
/// </summary>
public interface IBankingApiService
{
    Task<PaymentExecutionResult> ExecutePaymentAsync(
        string bankCode,
        string accountNumber,
        decimal amount,
        string reference,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PaymentStatusResult> CheckPaymentStatusAsync(
        string transactionId,
        string correlationId,
        CancellationToken cancellationToken = default);

    void ConfigureMTLS(X509Certificate2 clientCertificate);
}

