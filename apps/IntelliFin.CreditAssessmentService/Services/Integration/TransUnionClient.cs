using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IntelliFin.CreditAssessmentService.Services.Integration;

public class TransUnionClient : ITransUnionClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<TransUnionClient> _logger;

    public TransUnionClient(HttpClient httpClient, IDistributedCache? cache, ILogger<TransUnionClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CreditBureauData?> GetCreditReportAsync(string nrc, Guid clientId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching TransUnion credit report for NRC {Nrc}", nrc);

        // Check cache first
        var cacheKey = $"transunion:{nrc}";
        if (_cache != null)
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("TransUnion data found in cache for {Nrc}", nrc);
                return JsonSerializer.Deserialize<CreditBureauData>(cachedData);
            }
        }

        try
        {
            // TODO: Implement actual TransUnion API call
            // Stub response
            var bureauData = new CreditBureauData
            {
                Nrc = nrc,
                CreditScore = 680,
                TotalAccounts = 3,
                ActiveAccounts = 2,
                DefaultedAccounts = 0,
                TotalDebt = 45000,
                MonthlyObligations = 2800,
                ReportDate = DateTime.UtcNow,
                Accounts = new List<CreditAccount>
                {
                    new()
                    {
                        AccountNumber = "ACC001",
                        LenderName = "Standard Chartered",
                        AccountType = "Personal Loan",
                        CurrentBalance = 25000,
                        CreditLimit = 50000,
                        PaymentHistory = "Current",
                        DaysInArrears = 0
                    }
                }
            };

            // Cache the result for 90 days
            if (_cache != null)
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(90)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(bureauData), cacheOptions, cancellationToken);
            }

            return bureauData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching TransUnion data for NRC {Nrc}", nrc);
            return null;
        }
    }
}
