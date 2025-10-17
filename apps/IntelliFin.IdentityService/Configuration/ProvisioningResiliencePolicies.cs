using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace IntelliFin.IdentityService.Configuration;

/// <summary>
/// Resilience policies for user provisioning operations
/// </summary>
public static class ProvisioningResiliencePolicies
{
    /// <summary>
    /// Retry policy: 3 retries with exponential backoff (2^n seconds)
    /// </summary>
    public static AsyncRetryPolicy CreateRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Provisioning operation failed. Retry {RetryCount} after {Delay}ms. Error: {Error}",
                        retryCount,
                        timeSpan.TotalMilliseconds,
                        exception.Message);
                });
    }

    /// <summary>
    /// Circuit breaker policy: Opens after 5 consecutive failures for 1 minute
    /// </summary>
    public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(ILogger logger)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    logger.LogError(
                        exception,
                        "Circuit breaker opened for {Duration}ms due to {ExceptionType}: {Error}",
                        duration.TotalMilliseconds,
                        exception.GetType().Name,
                        exception.Message);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open, testing with next request");
                });
    }

    /// <summary>
    /// Combined policy: Circuit breaker wraps retry policy
    /// </summary>
    public static IAsyncPolicy CreateCombinedPolicy(ILogger logger)
    {
        var retryPolicy = CreateRetryPolicy(logger);
        var circuitBreakerPolicy = CreateCircuitBreakerPolicy(logger);

        // Circuit breaker wraps retry - if retry fails 5 times, circuit opens
        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
    }
}
