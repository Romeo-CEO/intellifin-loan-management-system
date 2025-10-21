using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Manual AML screening implementation
/// Uses hardcoded sanctions/PEP lists for Phase 1
/// Will be replaced with external API integration in future
/// </summary>
public class ManualAmlScreeningService : IAmlScreeningService
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<ManualAmlScreeningService> _logger;

    // Hardcoded sanctions list (OFAC/UN examples - for demo only)
    private static readonly HashSet<string> SanctionsList = new(StringComparer.OrdinalIgnoreCase)
    {
        "Vladimir Putin",
        "Kim Jong Un",
        "Bashar al-Assad",
        "Nicolas Maduro",
        "Sanctioned Person" // Test name
    };

    // Hardcoded PEP list (for demo only)
    private static readonly HashSet<string> PepList = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hakainde Hichilema", // President of Zambia
        "Mutale Nalumango", // Vice President
        "Political Figure", // Test name
        "Government Official" // Test name
    };

    public ManualAmlScreeningService(
        ClientManagementDbContext context,
        ILogger<ManualAmlScreeningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<AmlScreeningResult>> PerformScreeningAsync(
        Guid clientId,
        Guid kycStatusId,
        string screenedBy,
        string? correlationId = null)
    {
        try
        {
            // Load client
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null)
            {
                return Result<AmlScreeningResult>.Failure($"Client not found: {clientId}");
            }

            var clientFullName = $"{client.FirstName} {client.LastName}";

            _logger.LogInformation(
                "Starting AML screening for client {ClientId} ({ClientName})",
                clientId, clientFullName);

            var screenings = new List<AmlScreening>();

            // Perform sanctions screening
            var sanctionsResult = await PerformSanctionsScreeningAsync(
                kycStatusId, clientFullName, screenedBy, correlationId);
            screenings.Add(sanctionsResult);

            // Perform PEP screening
            var pepResult = await PerformPepScreeningAsync(
                kycStatusId, clientFullName, screenedBy, correlationId);
            screenings.Add(pepResult);

            // Calculate overall risk level
            var overallRisk = CalculateOverallRisk(sanctionsResult, pepResult);

            // Save screening results
            _context.AmlScreenings.AddRange(screenings);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "AML screening completed for client {ClientId}: Risk={RiskLevel}, " +
                "Sanctions={SanctionsHit}, PEP={PepMatch}",
                clientId, overallRisk, sanctionsResult.IsMatch, pepResult.IsMatch);

            // Create result
            var result = new AmlScreeningResult
            {
                OverallRiskLevel = overallRisk,
                SanctionsHit = sanctionsResult.IsMatch,
                PepMatch = pepResult.IsMatch,
                Screenings = screenings,
                Variables = new Dictionary<string, object>
                {
                    ["amlRiskLevel"] = overallRisk,
                    ["sanctionsHit"] = sanctionsResult.IsMatch,
                    ["pepMatch"] = pepResult.IsMatch,
                    ["amlScreeningComplete"] = true
                }
            };

            return Result<AmlScreeningResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing AML screening for client {ClientId}", clientId);
            return Result<AmlScreeningResult>.Failure($"AML screening failed: {ex.Message}");
        }
    }

    public async Task<AmlScreening> PerformSanctionsScreeningAsync(
        Guid kycStatusId,
        string clientName,
        string screenedBy,
        string? correlationId = null)
    {
        // Check against sanctions list
        var isMatch = SanctionsList.Any(sanctionedName =>
            clientName.Contains(sanctionedName, StringComparison.OrdinalIgnoreCase));

        var screening = new AmlScreening
        {
            Id = Guid.NewGuid(),
            KycStatusId = kycStatusId,
            ScreeningType = AmlScreeningType.Sanctions,
            ScreeningProvider = "Manual",
            ScreenedAt = DateTime.UtcNow,
            ScreenedBy = screenedBy,
            IsMatch = isMatch,
            RiskLevel = isMatch ? AmlRiskLevel.High : AmlRiskLevel.Clear,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };

        if (isMatch)
        {
            var matchedName = SanctionsList.FirstOrDefault(s =>
                clientName.Contains(s, StringComparison.OrdinalIgnoreCase));

            screening.MatchDetails = JsonSerializer.Serialize(new
            {
                matchedName = matchedName,
                listName = "OFAC/UN Sanctions",
                matchType = "NameMatch",
                matchScore = 0.95
            });

            screening.Notes = $"Potential sanctions match: {matchedName}";

            _logger.LogWarning(
                "Sanctions hit detected for KycStatus {KycStatusId}: {MatchedName}",
                kycStatusId, matchedName);
        }

        return await Task.FromResult(screening);
    }

    public async Task<AmlScreening> PerformPepScreeningAsync(
        Guid kycStatusId,
        string clientName,
        string screenedBy,
        string? correlationId = null)
    {
        // Check against PEP list
        var isMatch = PepList.Any(pepName =>
            clientName.Contains(pepName, StringComparison.OrdinalIgnoreCase));

        var screening = new AmlScreening
        {
            Id = Guid.NewGuid(),
            KycStatusId = kycStatusId,
            ScreeningType = AmlScreeningType.PEP,
            ScreeningProvider = "Manual",
            ScreenedAt = DateTime.UtcNow,
            ScreenedBy = screenedBy,
            IsMatch = isMatch,
            RiskLevel = isMatch ? AmlRiskLevel.High : AmlRiskLevel.Clear,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };

        if (isMatch)
        {
            var matchedName = PepList.FirstOrDefault(p =>
                clientName.Contains(p, StringComparison.OrdinalIgnoreCase));

            screening.MatchDetails = JsonSerializer.Serialize(new
            {
                matchedName = matchedName,
                pepType = "Politically Exposed Person",
                position = "Government Official",
                matchScore = 0.90
            });

            screening.Notes = $"Potential PEP match: {matchedName}";

            _logger.LogWarning(
                "PEP match detected for KycStatus {KycStatusId}: {MatchedName}",
                kycStatusId, matchedName);
        }

        return await Task.FromResult(screening);
    }

    private static string CalculateOverallRisk(AmlScreening sanctionsResult, AmlScreening pepResult)
    {
        // If either screening shows a match, risk is High
        if (sanctionsResult.IsMatch || pepResult.IsMatch)
        {
            return AmlRiskLevel.High;
        }

        // Otherwise, risk is Clear
        return AmlRiskLevel.Clear;
    }
}
