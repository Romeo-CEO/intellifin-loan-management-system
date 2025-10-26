using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IntelliFin.CreditAssessmentService.Services.Integration;

public class PmecClient : IPmecClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<PmecClient> _logger;

    public PmecClient(HttpClient httpClient, IDistributedCache? cache, ILogger<PmecClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PmecEmployeeData?> VerifyEmployeeAsync(string nrc, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying government employee for NRC {Nrc}", nrc);

        try
        {
            // TODO: Implement actual PMEC API call
            return await Task.FromResult(new PmecEmployeeData
            {
                Nrc = nrc,
                IsVerified = true,
                EmployerCode = "GOV-001",
                EmployerName = "Ministry of Finance",
                JobTitle = "Senior Officer",
                EmploymentStartDate = DateTime.UtcNow.AddYears(-2),
                EmploymentMonths = 24
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying employee for NRC {Nrc}", nrc);
            return null;
        }
    }

    public async Task<PmecSalaryData?> GetSalaryDataAsync(string nrc, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching PMEC salary data for NRC {Nrc}", nrc);

        var cacheKey = $"pmec:salary:{nrc}";
        if (_cache != null)
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonSerializer.Deserialize<PmecSalaryData>(cachedData);
            }
        }

        try
        {
            // TODO: Implement actual PMEC API call
            var salaryData = new PmecSalaryData
            {
                Nrc = nrc,
                GrossSalary = 18000,
                NetSalary = 15000,
                TotalDeductions = 3000,
                ExistingDeductions = new List<PmecDeduction>
                {
                    new()
                    {
                        DeductionCode = "LOAN-001",
                        DeductionName = "Existing Loan",
                        Amount = 2000,
                        StartDate = DateTime.UtcNow.AddMonths(-6)
                    }
                }
            };

            // Cache for 24 hours
            if (_cache != null)
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(salaryData), cacheOptions, cancellationToken);
            }

            return salaryData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching salary data for NRC {Nrc}", nrc);
            return null;
        }
    }
}
