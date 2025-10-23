using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Models;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Vault-based risk scoring service
/// Computes client risk scores using Vault-managed rules
/// </summary>
public class VaultRiskScoringService : IRiskScoringService
{
    private readonly ClientManagementDbContext _context;
    private readonly IRiskConfigProvider _configProvider;
    private readonly RulesExecutionEngine _rulesEngine;
    private readonly ILogger<VaultRiskScoringService> _logger;

    public VaultRiskScoringService(
        ClientManagementDbContext context,
        IRiskConfigProvider configProvider,
        RulesExecutionEngine rulesEngine,
        ILogger<VaultRiskScoringService> logger)
    {
        _context = context;
        _configProvider = configProvider;
        _rulesEngine = rulesEngine;
        _logger = logger;
    }

    public async Task<Result<RiskProfile>> ComputeRiskAsync(
        Guid clientId,
        string computedBy,
        string? correlationId = null)
    {
        try
        {
            _logger.LogInformation(
                "Computing risk for client {ClientId}",
                clientId);

            // Get current Vault configuration
            var configResult = await _configProvider.GetCurrentConfigAsync();
            if (configResult.IsFailure)
            {
                return Result<RiskProfile>.Failure($"Failed to retrieve risk configuration: {configResult.Error}");
            }

            var config = configResult.Value!;

            // Build input factors from client data
            var factorsResult = await BuildInputFactorsAsync(clientId);
            if (factorsResult.IsFailure)
            {
                return Result<RiskProfile>.Failure($"Failed to build input factors: {factorsResult.Error}");
            }

            var inputFactors = factorsResult.Value!;
            inputFactors.CorrelationId = correlationId;

            // Execute rules engine
            var executionResult = await _rulesEngine.EvaluateRulesAsync(config, inputFactors);

            // Determine risk rating from score
            var rating = DetermineRating(executionResult.TotalScore, config.Thresholds);

            // Supersede current risk profile if exists
            await SupersedeCurrentRiskProfileAsync(clientId, "NewAssessment");

            // Create new risk profile
            var riskProfile = new RiskProfile
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                RiskRating = rating,
                RiskScore = executionResult.TotalScore,
                ComputedAt = DateTime.UtcNow,
                ComputedBy = computedBy,
                RiskRulesVersion = config.Version,
                RiskRulesChecksum = config.Checksum,
                RuleExecutionLog = JsonSerializer.Serialize(executionResult.ExecutionLog),
                InputFactorsJson = JsonSerializer.Serialize(inputFactors),
                IsCurrent = true
            };

            _context.RiskProfiles.Add(riskProfile);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Risk computed for client {ClientId}: Score={Score}, Rating={Rating}, Rules={Version}",
                clientId, riskProfile.RiskScore, riskProfile.RiskRating, config.Version);

            return Result<RiskProfile>.Success(riskProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing risk for client {ClientId}", clientId);
            return Result<RiskProfile>.Failure($"Risk computation failed: {ex.Message}");
        }
    }

    public async Task<Result<RiskProfile>> RecomputeRiskAsync(
        Guid clientId,
        string reason,
        string computedBy)
    {
        _logger.LogInformation(
            "Recomputing risk for client {ClientId}: Reason={Reason}",
            clientId, reason);

        // Supersede with specific reason
        await SupersedeCurrentRiskProfileAsync(clientId, reason);

        // Compute new risk
        return await ComputeRiskAsync(clientId, computedBy);
    }

    public async Task<Result<InputFactors>> BuildInputFactorsAsync(Guid clientId)
    {
        try
        {
            // Load client
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null)
            {
                return Result<InputFactors>.Failure($"Client not found: {clientId}");
            }

            // Load KYC status
            var kycStatus = await _context.KycStatuses
                .FirstOrDefaultAsync(k => k.ClientId == clientId);

            // Load AML screenings
            var amlScreenings = await _context.AmlScreenings
                .Where(a => a.KycStatusId == kycStatus!.Id)
                .ToListAsync();

            // Load documents
            var documents = await _context.ClientDocuments
                .Where(d => d.ClientId == clientId && d.UploadStatus == Domain.Enums.UploadStatus.Verified)
                .CountAsync();

            // Build input factors
            var factors = new InputFactors
            {
                // KYC factors
                KycComplete = kycStatus?.IsDocumentComplete ?? false,
                KycState = kycStatus?.CurrentState.ToString() ?? "Pending",

                // AML factors
                AmlRiskLevel = DetermineAmlRiskLevel(amlScreenings),
                IsPep = amlScreenings.Any(a => a.ScreeningType == "PEP" && a.IsMatch),
                HasSanctionsHit = amlScreenings.Any(a => a.ScreeningType == "Sanctions" && a.IsMatch),
                AmlScreeningComplete = kycStatus?.AmlScreeningComplete ?? false,

                // Document factors
                DocumentCount = documents,
                AllDocumentsVerified = kycStatus?.IsDocumentComplete ?? false,
                HasNrc = kycStatus?.HasNrc ?? false,
                HasProofOfAddress = kycStatus?.HasProofOfAddress ?? false,

                // Client profile factors
                Age = DateTime.UtcNow.Year - client.DateOfBirth.Year,
                IsHighValue = false, // Will be enhanced in future with transaction data
                Province = client.Province ?? string.Empty,
                HasEmployer = !string.IsNullOrWhiteSpace(client.Employer),
                SourceOfFunds = client.SourceOfFunds ?? string.Empty,

                // EDD factors
                RequiresEdd = kycStatus?.RequiresEdd ?? false,
                EddReason = kycStatus?.EddReason,

                // Metadata
                ComputedAt = DateTime.UtcNow
            };

            return Result<InputFactors>.Success(factors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building input factors for client {ClientId}", clientId);
            return Result<InputFactors>.Failure($"Failed to build input factors: {ex.Message}");
        }
    }

    public async Task<List<RiskProfile>> GetRiskHistoryAsync(Guid clientId)
    {
        return await _context.RiskProfiles
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.ComputedAt)
            .ToListAsync();
    }

    public async Task<RiskProfile?> GetCurrentRiskProfileAsync(Guid clientId)
    {
        return await _context.RiskProfiles
            .FirstOrDefaultAsync(r => r.ClientId == clientId && r.IsCurrent);
    }

    private async Task SupersedeCurrentRiskProfileAsync(Guid clientId, string reason)
    {
        var currentProfile = await _context.RiskProfiles
            .FirstOrDefaultAsync(r => r.ClientId == clientId && r.IsCurrent);

        if (currentProfile != null)
        {
            currentProfile.IsCurrent = false;
            currentProfile.SupersededAt = DateTime.UtcNow;
            currentProfile.SupersededReason = reason;

            _logger.LogDebug(
                "Superseded risk profile {ProfileId} for client {ClientId}: Reason={Reason}",
                currentProfile.Id, clientId, reason);
        }
    }

    private string DetermineRating(int score, Dictionary<string, RiskThreshold> thresholds)
    {
        // Find matching threshold
        var matchingThreshold = thresholds.Values
            .FirstOrDefault(t => t.ContainsScore(score));

        if (matchingThreshold != null)
        {
            return matchingThreshold.Rating;
        }

        // Fallback rating
        _logger.LogWarning("No matching threshold for score {Score}, using fallback", score);
        return score switch
        {
            <= 25 => RiskRating.Low,
            <= 50 => RiskRating.Medium,
            _ => RiskRating.High
        };
    }

    private string DetermineAmlRiskLevel(List<AmlScreening> screenings)
    {
        if (!screenings.Any())
            return "Clear";

        var hasHighRisk = screenings.Any(s => s.RiskLevel == "High");
        var hasMediumRisk = screenings.Any(s => s.RiskLevel == "Medium");

        if (hasHighRisk)
            return "High";
        if (hasMediumRisk)
            return "Medium";

        return screenings.Any(s => s.RiskLevel == "Low") ? "Low" : "Clear";
    }
}
