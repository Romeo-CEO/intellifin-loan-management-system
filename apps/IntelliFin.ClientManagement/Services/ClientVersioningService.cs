using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service implementation for client versioning operations (SCD-2 temporal tracking)
/// </summary>
public class ClientVersioningService : IClientVersioningService
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<ClientVersioningService> _logger;

    public ClientVersioningService(ClientManagementDbContext context, ILogger<ClientVersioningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ClientVersionResponse>> CreateVersionAsync(
        Client client,
        string changeReason,
        string userId,
        string? ipAddress = null,
        string? correlationId = null)
    {
        try
        {
            // Get the last version number for this client
            var lastVersionNumber = await _context.ClientVersions
                .Where(cv => cv.ClientId == client.Id)
                .MaxAsync(cv => (int?)cv.VersionNumber) ?? 0;

            var newVersionNumber = lastVersionNumber + 1;

            // Get previous version for change summary calculation
            ClientVersion? previousVersion = null;
            if (lastVersionNumber > 0)
            {
                previousVersion = await _context.ClientVersions
                    .Where(cv => cv.ClientId == client.Id && cv.VersionNumber == lastVersionNumber)
                    .FirstOrDefaultAsync();
            }

            // Calculate change summary
            var changeSummary = CalculateChangeSummary(previousVersion, client, changeReason);

            var version = new ClientVersion
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                VersionNumber = newVersionNumber,
                
                // Full snapshot of client data
                Nrc = client.Nrc,
                PayrollNumber = client.PayrollNumber,
                FirstName = client.FirstName,
                LastName = client.LastName,
                OtherNames = client.OtherNames,
                DateOfBirth = client.DateOfBirth,
                Gender = client.Gender,
                MaritalStatus = client.MaritalStatus,
                Nationality = client.Nationality,
                Ministry = client.Ministry,
                EmployerType = client.EmployerType,
                EmploymentStatus = client.EmploymentStatus,
                PrimaryPhone = client.PrimaryPhone,
                SecondaryPhone = client.SecondaryPhone,
                Email = client.Email,
                PhysicalAddress = client.PhysicalAddress,
                City = client.City,
                Province = client.Province,
                KycStatus = client.KycStatus,
                KycCompletedAt = client.KycCompletedAt,
                KycCompletedBy = client.KycCompletedBy,
                AmlRiskLevel = client.AmlRiskLevel,
                IsPep = client.IsPep,
                IsSanctioned = client.IsSanctioned,
                RiskRating = client.RiskRating,
                RiskLastAssessedAt = client.RiskLastAssessedAt,
                Status = client.Status,
                BranchId = client.BranchId,
                
                // Temporal tracking
                ValidFrom = DateTime.UtcNow,
                ValidTo = null,
                IsCurrent = true,
                
                // Change tracking
                ChangeSummaryJson = changeSummary,
                ChangeReason = changeReason,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IpAddress = ipAddress,
                CorrelationId = correlationId
            };

            _context.ClientVersions.Add(version);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created version {VersionNumber} for client {ClientId} by user {UserId}",
                newVersionNumber, client.Id, userId);

            return Result<ClientVersionResponse>.Success(MapToResponse(version));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating version for client {ClientId}", client.Id);
            return Result<ClientVersionResponse>.Failure($"Error creating version: {ex.Message}");
        }
    }

    public async Task<Result<List<ClientVersionResponse>>> GetVersionHistoryAsync(Guid clientId)
    {
        try
        {
            var versions = await _context.ClientVersions
                .Where(cv => cv.ClientId == clientId)
                .OrderByDescending(cv => cv.VersionNumber)
                .ToListAsync();

            if (!versions.Any())
            {
                _logger.LogWarning("No version history found for client {ClientId}", clientId);
                return Result<List<ClientVersionResponse>>.Failure($"No version history found for client {clientId}");
            }

            var response = versions.Select(MapToResponse).ToList();
            return Result<List<ClientVersionResponse>>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version history for client {ClientId}", clientId);
            return Result<List<ClientVersionResponse>>.Failure($"Error retrieving version history: {ex.Message}");
        }
    }

    public async Task<Result<ClientVersionResponse>> GetVersionByNumberAsync(Guid clientId, int versionNumber)
    {
        try
        {
            var version = await _context.ClientVersions
                .FirstOrDefaultAsync(cv => cv.ClientId == clientId && cv.VersionNumber == versionNumber);

            if (version == null)
            {
                _logger.LogWarning("Version {VersionNumber} not found for client {ClientId}", versionNumber, clientId);
                return Result<ClientVersionResponse>.Failure($"Version {versionNumber} not found for client {clientId}");
            }

            return Result<ClientVersionResponse>.Success(MapToResponse(version));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version {VersionNumber} for client {ClientId}", versionNumber, clientId);
            return Result<ClientVersionResponse>.Failure($"Error retrieving version: {ex.Message}");
        }
    }

    public async Task<Result<ClientVersionResponse>> GetVersionAtTimestampAsync(Guid clientId, DateTime asOfDate)
    {
        try
        {
            var version = await _context.ClientVersions
                .Where(cv => cv.ClientId == clientId 
                    && cv.ValidFrom <= asOfDate 
                    && (cv.ValidTo == null || cv.ValidTo > asOfDate))
                .FirstOrDefaultAsync();

            if (version == null)
            {
                _logger.LogWarning("No version found for client {ClientId} at timestamp {AsOfDate}", clientId, asOfDate);
                return Result<ClientVersionResponse>.Failure($"No version found for client {clientId} at timestamp {asOfDate:O}");
            }

            return Result<ClientVersionResponse>.Success(MapToResponse(version));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version at timestamp {AsOfDate} for client {ClientId}", asOfDate, clientId);
            return Result<ClientVersionResponse>.Failure($"Error retrieving version at timestamp: {ex.Message}");
        }
    }

    public async Task<Result> CloseCurrentVersionAsync(Guid clientId)
    {
        try
        {
            var currentVersion = await _context.ClientVersions
                .FirstOrDefaultAsync(cv => cv.ClientId == clientId && cv.IsCurrent);

            if (currentVersion == null)
            {
                _logger.LogWarning("No current version found for client {ClientId}", clientId);
                return Result.Failure($"No current version found for client {clientId}");
            }

            currentVersion.IsCurrent = false;
            currentVersion.ValidTo = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Closed current version {VersionNumber} for client {ClientId}", 
                currentVersion.VersionNumber, clientId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing current version for client {ClientId}", clientId);
            return Result.Failure($"Error closing current version: {ex.Message}");
        }
    }

    private static string CalculateChangeSummary(ClientVersion? previousVersion, Client currentClient, string reason)
    {
        if (previousVersion == null)
        {
            // First version - no changes to compare
            return JsonSerializer.Serialize(new
            {
                fields = new[] { "Initial Version" },
                changes = Array.Empty<object>(),
                reason,
                timestamp = DateTime.UtcNow
            });
        }

        var changedFields = new List<string>();
        var changes = new List<object>();

        // Compare all mutable fields
        CompareField("FirstName", previousVersion.FirstName, currentClient.FirstName, changedFields, changes);
        CompareField("LastName", previousVersion.LastName, currentClient.LastName, changedFields, changes);
        CompareField("OtherNames", previousVersion.OtherNames, currentClient.OtherNames, changedFields, changes);
        CompareField("MaritalStatus", previousVersion.MaritalStatus, currentClient.MaritalStatus, changedFields, changes);
        CompareField("PrimaryPhone", previousVersion.PrimaryPhone, currentClient.PrimaryPhone, changedFields, changes);
        CompareField("SecondaryPhone", previousVersion.SecondaryPhone, currentClient.SecondaryPhone, changedFields, changes);
        CompareField("Email", previousVersion.Email, currentClient.Email, changedFields, changes);
        CompareField("PhysicalAddress", previousVersion.PhysicalAddress, currentClient.PhysicalAddress, changedFields, changes);
        CompareField("City", previousVersion.City, currentClient.City, changedFields, changes);
        CompareField("Province", previousVersion.Province, currentClient.Province, changedFields, changes);
        CompareField("Ministry", previousVersion.Ministry, currentClient.Ministry, changedFields, changes);
        CompareField("EmployerType", previousVersion.EmployerType, currentClient.EmployerType, changedFields, changes);
        CompareField("EmploymentStatus", previousVersion.EmploymentStatus, currentClient.EmploymentStatus, changedFields, changes);
        CompareField("KycStatus", previousVersion.KycStatus, currentClient.KycStatus, changedFields, changes);
        CompareField("AmlRiskLevel", previousVersion.AmlRiskLevel, currentClient.AmlRiskLevel, changedFields, changes);
        CompareField("RiskRating", previousVersion.RiskRating, currentClient.RiskRating, changedFields, changes);
        CompareField("Status", previousVersion.Status, currentClient.Status, changedFields, changes);

        if (previousVersion.IsPep != currentClient.IsPep)
        {
            changedFields.Add("IsPep");
            changes.Add(new { field = "IsPep", oldValue = previousVersion.IsPep, newValue = currentClient.IsPep });
        }

        if (previousVersion.IsSanctioned != currentClient.IsSanctioned)
        {
            changedFields.Add("IsSanctioned");
            changes.Add(new { field = "IsSanctioned", oldValue = previousVersion.IsSanctioned, newValue = currentClient.IsSanctioned });
        }

        return JsonSerializer.Serialize(new
        {
            fields = changedFields.ToArray(),
            changes = changes.ToArray(),
            reason,
            timestamp = DateTime.UtcNow
        });
    }

    private static void CompareField(string fieldName, string? oldValue, string? newValue, List<string> changedFields, List<object> changes)
    {
        if (oldValue != newValue)
        {
            changedFields.Add(fieldName);
            changes.Add(new { field = fieldName, oldValue = oldValue ?? "", newValue = newValue ?? "" });
        }
    }

    private static ClientVersionResponse MapToResponse(ClientVersion version)
    {
        return new ClientVersionResponse
        {
            Id = version.Id,
            ClientId = version.ClientId,
            VersionNumber = version.VersionNumber,
            Nrc = version.Nrc,
            PayrollNumber = version.PayrollNumber,
            FirstName = version.FirstName,
            LastName = version.LastName,
            OtherNames = version.OtherNames,
            DateOfBirth = version.DateOfBirth,
            Gender = version.Gender,
            MaritalStatus = version.MaritalStatus,
            Nationality = version.Nationality,
            Ministry = version.Ministry,
            EmployerType = version.EmployerType,
            EmploymentStatus = version.EmploymentStatus,
            PrimaryPhone = version.PrimaryPhone,
            SecondaryPhone = version.SecondaryPhone,
            Email = version.Email,
            PhysicalAddress = version.PhysicalAddress,
            City = version.City,
            Province = version.Province,
            KycStatus = version.KycStatus,
            KycCompletedAt = version.KycCompletedAt,
            KycCompletedBy = version.KycCompletedBy,
            AmlRiskLevel = version.AmlRiskLevel,
            IsPep = version.IsPep,
            IsSanctioned = version.IsSanctioned,
            RiskRating = version.RiskRating,
            RiskLastAssessedAt = version.RiskLastAssessedAt,
            Status = version.Status,
            BranchId = version.BranchId,
            ValidFrom = version.ValidFrom,
            ValidTo = version.ValidTo,
            IsCurrent = version.IsCurrent,
            ChangeSummaryJson = version.ChangeSummaryJson,
            ChangeReason = version.ChangeReason,
            CreatedAt = version.CreatedAt,
            CreatedBy = version.CreatedBy,
            IpAddress = version.IpAddress,
            CorrelationId = version.CorrelationId
        };
    }
}
