using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Data;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Manual AML screening implementation with fuzzy matching
/// Uses comprehensive sanctions/PEP lists for Phase 1
/// Will be replaced with external API integration in future
/// </summary>
public class ManualAmlScreeningService : IAmlScreeningService
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<ManualAmlScreeningService> _logger;
    private readonly FuzzyNameMatcher _fuzzyMatcher;

    /// <summary>
    /// Confidence threshold for sanctions match (stricter)
    /// </summary>
    private const int SanctionsConfidenceThreshold = 70;

    /// <summary>
    /// Confidence threshold for PEP match (moderate)
    /// </summary>
    private const int PepConfidenceThreshold = 60;

    public ManualAmlScreeningService(
        ClientManagementDbContext context,
        ILogger<ManualAmlScreeningService> logger,
        FuzzyNameMatcher fuzzyMatcher)
    {
        _context = context;
        _logger = logger;
        _fuzzyMatcher = fuzzyMatcher;
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
        // Get all sanctions entities
        var allSanctions = SanctionsList.GetAllSanctions();

        // Find best match using fuzzy matching
        MatchResult? bestMatch = null;
        SanctionedEntity? matchedEntity = null;

        foreach (var sanctioned in allSanctions)
        {
            // Check against all names (primary + aliases)
            var matchResult = _fuzzyMatcher.CalculateMatch(
                clientName,
                sanctioned.Name,
                sanctioned.Aliases);

            if (matchResult.Confidence > (bestMatch?.Confidence ?? 0))
            {
                bestMatch = matchResult;
                matchedEntity = sanctioned;
            }
        }

        // Determine if match exceeds threshold
        var isMatch = bestMatch != null && bestMatch.Confidence >= SanctionsConfidenceThreshold;

        // Determine risk level based on confidence
        var riskLevel = isMatch
            ? (bestMatch!.Confidence >= 90 ? AmlRiskLevel.High : AmlRiskLevel.Medium)
            : AmlRiskLevel.Clear;

        var screening = new AmlScreening
        {
            Id = Guid.NewGuid(),
            KycStatusId = kycStatusId,
            ScreeningType = AmlScreeningType.Sanctions,
            ScreeningProvider = "Manual_v2_Fuzzy",
            ScreenedAt = DateTime.UtcNow,
            ScreenedBy = screenedBy,
            IsMatch = isMatch,
            RiskLevel = riskLevel,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };

        if (isMatch && bestMatch != null && matchedEntity != null)
        {
            screening.MatchDetails = JsonSerializer.Serialize(new
            {
                matchedName = bestMatch.MatchedName,
                primaryName = matchedEntity.Name,
                entityType = matchedEntity.EntityType,
                program = matchedEntity.Program,
                country = matchedEntity.Country,
                description = matchedEntity.Description,
                matchType = bestMatch.MatchType,
                matchConfidence = bestMatch.Confidence,
                levenshteinDistance = bestMatch.LevenshteinDistance,
                soundexMatch = bestMatch.SoundexMatch
            });

            screening.Notes = $"Sanctions match: {bestMatch.MatchedName} " +
                            $"({bestMatch.MatchType}, {bestMatch.Confidence}% confidence) " +
                            $"- {matchedEntity.Program}";

            _logger.LogWarning(
                "Sanctions hit detected for KycStatus {KycStatusId}: {MatchedName} " +
                "({Confidence}% confidence, {MatchType})",
                kycStatusId, bestMatch.MatchedName, bestMatch.Confidence, bestMatch.MatchType);
        }
        else if (bestMatch != null && bestMatch.Confidence >= 50)
        {
            // Log potential match below threshold for review
            _logger.LogInformation(
                "Low-confidence sanctions match for KycStatus {KycStatusId}: {ClientName} vs {MatchedName} " +
                "({Confidence}% - below {Threshold}% threshold)",
                kycStatusId, clientName, bestMatch.MatchedName, bestMatch.Confidence, SanctionsConfidenceThreshold);
        }

        return await Task.FromResult(screening);
    }

    public async Task<AmlScreening> PerformPepScreeningAsync(
        Guid kycStatusId,
        string clientName,
        string screenedBy,
        string? correlationId = null)
    {
        // Get all PEPs
        var allPeps = ZambianPepDatabase.GetActivePeps(); // Only active PEPs

        // Find best match using fuzzy matching
        MatchResult? bestMatch = null;
        PoliticallyExposedPerson? matchedPep = null;

        foreach (var pep in allPeps)
        {
            // Check against all names (primary + aliases)
            var matchResult = _fuzzyMatcher.CalculateMatch(
                clientName,
                pep.Name,
                pep.Aliases);

            if (matchResult.Confidence > (bestMatch?.Confidence ?? 0))
            {
                bestMatch = matchResult;
                matchedPep = pep;
            }
        }

        // Determine if match exceeds threshold
        var isMatch = bestMatch != null && bestMatch.Confidence >= PepConfidenceThreshold;

        // Determine risk level based on PEP risk level and confidence
        var riskLevel = AmlRiskLevel.Clear;
        if (isMatch && matchedPep != null)
        {
            // High-risk PEPs (President, Ministers, etc.) → High AML risk
            // Medium-risk PEPs (MPs, officials) → Medium AML risk
            riskLevel = matchedPep.RiskLevel switch
            {
                "High" => AmlRiskLevel.High,
                "Medium" => AmlRiskLevel.Medium,
                _ => AmlRiskLevel.Low
            };
        }

        var screening = new AmlScreening
        {
            Id = Guid.NewGuid(),
            KycStatusId = kycStatusId,
            ScreeningType = AmlScreeningType.PEP,
            ScreeningProvider = "Manual_v2_Fuzzy_Zambia",
            ScreenedAt = DateTime.UtcNow,
            ScreenedBy = screenedBy,
            IsMatch = isMatch,
            RiskLevel = riskLevel,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };

        if (isMatch && bestMatch != null && matchedPep != null)
        {
            screening.MatchDetails = JsonSerializer.Serialize(new
            {
                matchedName = bestMatch.MatchedName,
                primaryName = matchedPep.Name,
                position = matchedPep.Position,
                ministry = matchedPep.Ministry,
                pepCategory = matchedPep.PepCategory,
                pepRiskLevel = matchedPep.RiskLevel,
                isActive = matchedPep.IsActive,
                appointmentDate = matchedPep.AppointmentDate,
                matchType = bestMatch.MatchType,
                matchConfidence = bestMatch.Confidence,
                levenshteinDistance = bestMatch.LevenshteinDistance,
                soundexMatch = bestMatch.SoundexMatch
            });

            screening.Notes = $"PEP match: {bestMatch.MatchedName} " +
                            $"({bestMatch.MatchType}, {bestMatch.Confidence}% confidence) " +
                            $"- {matchedPep.Position} at {matchedPep.Ministry}";

            _logger.LogWarning(
                "PEP match detected for KycStatus {KycStatusId}: {MatchedName} " +
                "({Confidence}% confidence, {MatchType}) - {Position}",
                kycStatusId, bestMatch.MatchedName, bestMatch.Confidence, 
                bestMatch.MatchType, matchedPep.Position);
        }
        else if (bestMatch != null && bestMatch.Confidence >= 50)
        {
            // Log potential match below threshold for review
            _logger.LogInformation(
                "Low-confidence PEP match for KycStatus {KycStatusId}: {ClientName} vs {MatchedName} " +
                "({Confidence}% - below {Threshold}% threshold)",
                kycStatusId, clientName, bestMatch.MatchedName, bestMatch.Confidence, PepConfidenceThreshold);
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
