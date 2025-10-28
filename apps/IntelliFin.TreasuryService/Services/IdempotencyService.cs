using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.TreasuryService.Services;

/// <summary>
/// Service for handling idempotency and transaction safety
/// </summary>
public class IdempotencyService : IIdempotencyService
{
    private readonly ITreasuryTransactionRepository _transactionRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyService> _logger;

    // Cache keys for idempotency tracking
    private const string IdempotencyCacheKey = "idempotency_{0}";
    private const string TransactionCacheKey = "transaction_{0}";
    private const int CacheExpirationMinutes = 60; // 1 hour for idempotency tracking

    public IdempotencyService(
        ITreasuryTransactionRepository transactionRepository,
        IDistributedCache cache,
        ILogger<IdempotencyService> logger)
    {
        _transactionRepository = transactionRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Check if a request with the given idempotency key has already been processed
    /// </summary>
    public async Task<IdempotencyCheckResult> CheckIdempotencyAsync(string idempotencyKey)
    {
        _logger.LogDebug("Checking idempotency for key: {IdempotencyKey}", idempotencyKey);

        var cacheKey = string.Format(IdempotencyCacheKey, idempotencyKey);

        // Check cache first
        var cachedResult = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedResult))
        {
            var result = JsonSerializer.Deserialize<IdempotencyCheckResult>(cachedResult);
            if (result != null)
            {
                _logger.LogDebug("Found cached idempotency result: Key={IdempotencyKey}, Status={Status}", idempotencyKey, result.Status);
                return result;
            }
        }

        // Check database for existing transactions with this idempotency key
        var existingTransactions = await _transactionRepository.GetByCorrelationIdAsync(idempotencyKey);

        IdempotencyCheckResult result;
        if (existingTransactions.Any())
        {
            var transaction = existingTransactions.First();
            result = new IdempotencyCheckResult
            {
                IsDuplicate = true,
                Status = MapTransactionStatus(transaction.Status),
                TransactionId = transaction.TransactionId,
                OriginalTimestamp = transaction.CreatedAt,
                Message = $"Transaction already exists with status: {transaction.Status}"
            };
        }
        else
        {
            result = new IdempotencyCheckResult
            {
                IsDuplicate = false,
                Status = "NotProcessed",
                Message = "Request not found - can be processed"
            };
        }

        // Cache the result
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);

        _logger.LogDebug("Idempotency check completed: Key={IdempotencyKey}, IsDuplicate={IsDuplicate}", idempotencyKey, result.IsDuplicate);

        return result;
    }

    /// <summary>
    /// Mark a request as being processed to prevent duplicate processing
    /// </summary>
    public async Task MarkProcessingAsync(string idempotencyKey, Guid transactionId)
    {
        _logger.LogInformation("Marking request as processing: Key={IdempotencyKey}, TransactionId={TransactionId}", idempotencyKey, transactionId);

        var cacheKey = string.Format(IdempotencyCacheKey, idempotencyKey);
        var transactionCacheKey = string.Format(TransactionCacheKey, transactionId);

        var result = new IdempotencyCheckResult
        {
            IsDuplicate = false,
            Status = "Processing",
            TransactionId = transactionId,
            ProcessingStartedAt = DateTime.UtcNow,
            Message = "Request is currently being processed"
        };

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
        };

        // Store in both cache keys
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);
        await _cache.SetStringAsync(transactionCacheKey, JsonSerializer.Serialize(result), cacheOptions);
    }

    /// <summary>
    /// Mark a request as completed
    /// </summary>
    public async Task MarkCompletedAsync(string idempotencyKey, Guid transactionId, string finalStatus)
    {
        _logger.LogInformation("Marking request as completed: Key={IdempotencyKey}, TransactionId={TransactionId}, Status={FinalStatus}",
            idempotencyKey, transactionId, finalStatus);

        var cacheKey = string.Format(IdempotencyCacheKey, idempotencyKey);
        var transactionCacheKey = string.Format(TransactionCacheKey, transactionId);

        var result = new IdempotencyCheckResult
        {
            IsDuplicate = true,
            Status = finalStatus,
            TransactionId = transactionId,
            CompletedAt = DateTime.UtcNow,
            Message = $"Request completed with status: {finalStatus}"
        };

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
        };

        // Update both cache entries
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);
        await _cache.SetStringAsync(transactionCacheKey, JsonSerializer.Serialize(result), cacheOptions);
    }

    /// <summary>
    /// Mark a request as failed
    /// </summary>
    public async Task MarkFailedAsync(string idempotencyKey, Guid transactionId, string errorMessage)
    {
        _logger.LogError("Marking request as failed: Key={IdempotencyKey}, TransactionId={TransactionId}, Error={ErrorMessage}",
            idempotencyKey, transactionId, errorMessage);

        var cacheKey = string.Format(IdempotencyCacheKey, idempotencyKey);
        var transactionCacheKey = string.Format(TransactionCacheKey, transactionId);

        var result = new IdempotencyCheckResult
        {
            IsDuplicate = true,
            Status = "Failed",
            TransactionId = transactionId,
            FailedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            Message = $"Request failed: {errorMessage}"
        };

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
        };

        // Update both cache entries
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);
        await _cache.SetStringAsync(transactionCacheKey, JsonSerializer.Serialize(result), cacheOptions);
    }

    /// <summary>
    /// Clean up expired idempotency cache entries
    /// </summary>
    public async Task CleanupExpiredEntriesAsync()
    {
        // Note: In a real implementation, you might want to implement cache cleanup
        // For now, we rely on the expiration settings in the cache options
        _logger.LogDebug("Idempotency cache cleanup completed");
    }

    /// <summary>
    /// Generate a unique idempotency key for a disbursement request
    /// </summary>
    public string GenerateIdempotencyKey(string loanId, string clientId, decimal amount, DateTime requestedAt)
    {
        // Create a deterministic key based on the request parameters
        var keyData = $"{loanId}_{clientId}_{amount}_{requestedAt:yyyyMMddHHmmss}";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyData);

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(keyBytes);
        var hashString = Convert.ToHexString(hashBytes);

        return $"disbursement_{hashString[..16]}"; // Use first 16 chars of hash
    }

    /// <summary>
    /// Map database transaction status to idempotency status
    /// </summary>
    private static string MapTransactionStatus(string dbStatus)
    {
        return dbStatus switch
        {
            "Pending" => "Processing",
            "Processing" => "Processing",
            "Completed" => "Completed",
            "Failed" => "Failed",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Result of idempotency check
/// </summary>
public class IdempotencyCheckResult
{
    public bool IsDuplicate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime OriginalTimestamp { get; set; }
    public DateTime ProcessingStartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public DateTime FailedAt { get; set; }
}

/// <summary>
/// Interface for idempotency service
/// </summary>
public interface IIdempotencyService
{
    Task<IdempotencyCheckResult> CheckIdempotencyAsync(string idempotencyKey);
    Task MarkProcessingAsync(string idempotencyKey, Guid transactionId);
    Task MarkCompletedAsync(string idempotencyKey, Guid transactionId, string finalStatus);
    Task MarkFailedAsync(string idempotencyKey, Guid transactionId, string errorMessage);
    Task CleanupExpiredEntriesAsync();
    string GenerateIdempotencyKey(string loanId, string clientId, decimal amount, DateTime requestedAt);
}

