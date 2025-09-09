using IntelliFin.FinancialService.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;
using System.Net.Sockets;

namespace IntelliFin.FinancialService.Services;

public interface IPaymentRetryService
{
    Task<PaymentProcessingResult> ProcessPaymentWithRetryAsync(ProcessPaymentRequest request);
    Task<TinggPaymentResult> ProcessTinggPaymentWithRetryAsync(TinggPaymentRequest request);
    Task<DeductionSubmissionResult> SubmitPmecDeductionsWithRetryAsync(DeductionSubmissionRequest request);
    Task<PaymentStatusResult> GetPaymentStatusWithRetryAsync(string paymentId);
}

public class PaymentRetryService : IPaymentRetryService
{
    private readonly IPaymentProcessingService _paymentService;
    private readonly IPmecService _pmecService;
    private readonly ILogger<PaymentRetryService> _logger;
    private readonly IConfiguration _configuration;

    // Retry policies
    private readonly IAsyncPolicy _paymentRetryPolicy;
    private readonly IAsyncPolicy _tinggRetryPolicy;
    private readonly IAsyncPolicy _pmecRetryPolicy;
    private readonly IAsyncPolicy _statusRetryPolicy;

    public PaymentRetryService(
        IPaymentProcessingService paymentService,
        IPmecService pmecService,
        ILogger<PaymentRetryService> logger,
        IConfiguration configuration)
    {
        _paymentService = paymentService;
        _pmecService = pmecService;
        _logger = logger;
        _configuration = configuration;

        // Configure retry policies with exponential backoff
        _paymentRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: _configuration.GetValue<int>("PaymentRetry:MaxRetries", 3),
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Payment processing retry {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds);
                });

        _tinggRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: _configuration.GetValue<int>("Tingg:MaxRetries", 5),
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(1.5, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Tingg payment retry {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds);
                });

        _pmecRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: _configuration.GetValue<int>("PMEC:MaxRetries", 4),
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(2 * retryAttempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("PMEC deduction retry {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds);
                });

        _statusRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * retryAttempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Payment status retry {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds);
                });
    }

    public async Task<PaymentProcessingResult> ProcessPaymentWithRetryAsync(ProcessPaymentRequest request)
    {
        _logger.LogInformation("Processing payment with retry for loan {LoanId}, amount {Amount}", 
            request.LoanId, request.Amount);

        try
        {
            return await _paymentRetryPolicy.ExecuteAsync(async () =>
            {
                var result = await _paymentService.ProcessPaymentAsync(request);
                
                // If payment failed with retryable error, throw to trigger retry
                if (!result.Success && IsRetryablePaymentError(result))
                {
                    throw new PaymentRetryableException($"Payment processing failed: {result.Message}");
                }

                return result;
            });
        }
        catch (PaymentRetryableException ex)
        {
            _logger.LogError(ex, "Payment processing failed after all retries for loan {LoanId}", request.LoanId);
            return new PaymentProcessingResult
            {
                Success = false,
                Message = "Payment processing failed after multiple retries",
                Errors = new List<string> { ex.Message }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment processing with retry for loan {LoanId}", request.LoanId);
            return new PaymentProcessingResult
            {
                Success = false,
                Message = "Payment processing encountered an unexpected error",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<TinggPaymentResult> ProcessTinggPaymentWithRetryAsync(TinggPaymentRequest request)
    {
        _logger.LogInformation("Processing Tingg payment with retry for amount {Amount} to {PhoneNumber}",
            request.Amount, request.PhoneNumber);

        try
        {
            return await _tinggRetryPolicy.ExecuteAsync(async () =>
            {
                var result = await _paymentService.ProcessTinggPaymentAsync(request);
                
                // If Tingg payment failed with retryable error, throw to trigger retry
                if (!result.Success && IsRetryableTinggError(result))
                {
                    throw new TinggRetryableException($"Tingg payment failed: {result.Message}");
                }

                return result;
            });
        }
        catch (TinggRetryableException ex)
        {
            _logger.LogError(ex, "Tingg payment failed after all retries for transaction {TransactionId}", 
                request.MerchantTransactionId);
            return new TinggPaymentResult
            {
                Success = false,
                TransactionId = request.MerchantTransactionId,
                Status = PaymentStatus.Failed,
                Message = "Tingg payment failed after multiple retries",
                Errors = new List<string> { ex.Message }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Tingg payment with retry");
            return new TinggPaymentResult
            {
                Success = false,
                TransactionId = request.MerchantTransactionId,
                Status = PaymentStatus.Failed,
                Message = "Tingg payment encountered an unexpected error",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<DeductionSubmissionResult> SubmitPmecDeductionsWithRetryAsync(DeductionSubmissionRequest request)
    {
        _logger.LogInformation("Submitting PMEC deductions with retry for cycle {CycleId}", request.CycleId);

        try
        {
            return await _pmecRetryPolicy.ExecuteAsync(async () =>
            {
                var result = await _pmecService.SubmitDeductionsAsync(request);
                
                // If PMEC submission failed with retryable error, throw to trigger retry
                if (!result.Success && IsRetryablePmecError(result))
                {
                    throw new PmecRetryableException($"PMEC submission failed: {result.Message}");
                }

                return result;
            });
        }
        catch (PmecRetryableException ex)
        {
            _logger.LogError(ex, "PMEC deduction submission failed after all retries for cycle {CycleId}", 
                request.CycleId);
            return new DeductionSubmissionResult
            {
                Success = false,
                CycleId = request.CycleId,
                TotalItems = request.Items.Count,
                AcceptedItems = 0,
                RejectedItems = request.Items.Count,
                Message = "PMEC submission failed after multiple retries"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during PMEC submission with retry");
            return new DeductionSubmissionResult
            {
                Success = false,
                CycleId = request.CycleId,
                TotalItems = request.Items.Count,
                AcceptedItems = 0,
                RejectedItems = request.Items.Count,
                Message = "PMEC submission encountered an unexpected error"
            };
        }
    }

    public async Task<PaymentStatusResult> GetPaymentStatusWithRetryAsync(string paymentId)
    {
        _logger.LogInformation("Getting payment status with retry for payment {PaymentId}", paymentId);

        try
        {
            return await _statusRetryPolicy.ExecuteAsync(async () =>
            {
                return await _paymentService.GetPaymentStatusAsync(paymentId);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment status after retries for payment {PaymentId}", paymentId);
            return new PaymentStatusResult
            {
                PaymentId = paymentId,
                Status = PaymentStatus.Failed,
                StatusDescription = "Failed to retrieve payment status",
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    private bool IsRetryableException(Exception ex)
    {
        // Check for common retryable exceptions
        return ex is HttpRequestException ||
               ex is TaskCanceledException ||
               ex is SocketException ||
               (ex.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true) ||
               (ex.Message?.Contains("network", StringComparison.OrdinalIgnoreCase) == true);
    }

    private bool IsRetryablePaymentError(PaymentProcessingResult result)
    {
        // Define retryable error conditions
        var retryableMessages = new[]
        {
            "timeout",
            "network error",
            "service unavailable",
            "server error",
            "connection failed"
        };

        return result.Errors?.Any(error => 
            retryableMessages.Any(msg => error.Contains(msg, StringComparison.OrdinalIgnoreCase))) == true;
    }

    private bool IsRetryableTinggError(TinggPaymentResult result)
    {
        // Define retryable Tingg error conditions
        var retryableMessages = new[]
        {
            "gateway timeout",
            "service temporarily unavailable",
            "network error",
            "connection timeout"
        };

        return result.Errors?.Any(error => 
            retryableMessages.Any(msg => error.Contains(msg, StringComparison.OrdinalIgnoreCase))) == true;
    }

    private bool IsRetryablePmecError(DeductionSubmissionResult result)
    {
        // Define retryable PMEC error conditions
        var retryableMessages = new[]
        {
            "system unavailable",
            "timeout",
            "network error",
            "service maintenance"
        };

        return result.Message != null && 
               retryableMessages.Any(msg => result.Message.Contains(msg, StringComparison.OrdinalIgnoreCase));
    }
}

// Custom exceptions for retry scenarios
public class PaymentRetryableException : Exception
{
    public PaymentRetryableException(string message) : base(message) { }
    public PaymentRetryableException(string message, Exception innerException) : base(message, innerException) { }
}

public class TinggRetryableException : Exception
{
    public TinggRetryableException(string message) : base(message) { }
    public TinggRetryableException(string message, Exception innerException) : base(message, innerException) { }
}

public class PmecRetryableException : Exception
{
    public PmecRetryableException(string message) : base(message) { }
    public PmecRetryableException(string message, Exception innerException) : base(message, innerException) { }
}