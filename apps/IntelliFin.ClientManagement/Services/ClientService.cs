using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service implementation for client operations
/// </summary>
public class ClientService : IClientService
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<ClientService> _logger;
    private readonly IClientVersioningService _versioningService;
    private readonly IAuditService _auditService;

    public ClientService(
        ClientManagementDbContext context, 
        ILogger<ClientService> logger,
        IClientVersioningService versioningService,
        IAuditService auditService)
    {
        _context = context;
        _logger = logger;
        _versioningService = versioningService;
        _auditService = auditService;
    }

    public async Task<Result<ClientResponse>> CreateClientAsync(CreateClientRequest request, string userId)
    {
        try
        {
            // Check for duplicate NRC
            var existingClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.Nrc == request.Nrc);

            if (existingClient != null)
            {
                _logger.LogWarning("Attempt to create client with duplicate NRC: {Nrc}", request.Nrc);
                return Result<ClientResponse>.Failure($"Client with NRC {request.Nrc} already exists");
            }

            // Create new client entity
            var client = new Client
            {
                Id = Guid.NewGuid(),
                Nrc = request.Nrc,
                PayrollNumber = request.PayrollNumber,
                FirstName = request.FirstName,
                LastName = request.LastName,
                OtherNames = request.OtherNames,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                MaritalStatus = request.MaritalStatus,
                Nationality = request.Nationality ?? "Zambian",
                Ministry = request.Ministry,
                EmployerType = request.EmployerType,
                EmploymentStatus = request.EmploymentStatus,
                PrimaryPhone = request.PrimaryPhone,
                SecondaryPhone = request.SecondaryPhone,
                Email = request.Email,
                PhysicalAddress = request.PhysicalAddress,
                City = request.City,
                Province = request.Province,
                BranchId = request.BranchId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId,
                Status = "Active",
                KycStatus = "Pending",
                AmlRiskLevel = "Low",
                RiskRating = "Low",
                IsPep = false,
                IsSanctioned = false,
                VersionNumber = 1
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            // Create initial version snapshot (version 1)
            var versionResult = await _versioningService.CreateVersionAsync(
                client,
                "Initial client creation",
                userId,
                null, // IP address - could be added later
                null  // Correlation ID - could be added later
            );

            if (versionResult.IsFailure)
            {
                _logger.LogWarning("Failed to create initial version for client {ClientId}: {Error}", 
                    client.Id, versionResult.Error);
                // Don't fail client creation if versioning fails, but log it
            }

            _logger.LogInformation("Created client {ClientId} with NRC {Nrc} by user {UserId}", 
                client.Id, client.Nrc, userId);

            // Fire-and-forget audit event (do not block request)
            _ = _auditService.LogAuditEventAsync(
                action: "ClientCreated",
                entityType: "Client",
                entityId: client.Id.ToString(),
                actor: userId,
                eventData: new { nrc = client.Nrc, firstName = client.FirstName, lastName = client.LastName, branchId = client.BranchId }
            );

            return Result<ClientResponse>.Success(MapToResponse(client));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client with NRC {Nrc}", request.Nrc);
            return Result<ClientResponse>.Failure($"Error creating client: {ex.Message}");
        }
    }

    public async Task<Result<ClientResponse>> GetClientByIdAsync(Guid id)
    {
        try
        {
            var client = await _context.Clients.FindAsync(id);

            if (client == null)
            {
                _logger.LogWarning("Client not found with ID: {ClientId}", id);
                return Result<ClientResponse>.Failure($"Client with ID {id} not found");
            }

            return Result<ClientResponse>.Success(MapToResponse(client));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientId}", id);
            return Result<ClientResponse>.Failure($"Error retrieving client: {ex.Message}");
        }
    }

    public async Task<Result<ClientResponse>> GetClientByNrcAsync(string nrc)
    {
        try
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Nrc.ToLower() == nrc.ToLower());

            if (client == null)
            {
                _logger.LogWarning("Client not found with NRC: {Nrc}", nrc);
                return Result<ClientResponse>.Failure($"Client with NRC {nrc} not found");
            }

            return Result<ClientResponse>.Success(MapToResponse(client));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client by NRC {Nrc}", nrc);
            return Result<ClientResponse>.Failure($"Error retrieving client: {ex.Message}");
        }
    }

    public async Task<Result<ClientResponse>> UpdateClientAsync(Guid id, UpdateClientRequest request, string userId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var client = await _context.Clients.FindAsync(id);

            if (client == null)
            {
                _logger.LogWarning("Client not found for update: {ClientId}", id);
                return Result<ClientResponse>.Failure($"Client with ID {id} not found");
            }

            // Close current version (set IsCurrent=false, ValidTo=NOW)
            var closeResult = await _versioningService.CloseCurrentVersionAsync(client.Id);
            if (closeResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return Result<ClientResponse>.Failure(closeResult.Error);
            }

            // Update mutable fields only
            client.FirstName = request.FirstName;
            client.LastName = request.LastName;
            client.OtherNames = request.OtherNames;
            client.MaritalStatus = request.MaritalStatus;
            client.PrimaryPhone = request.PrimaryPhone;
            client.SecondaryPhone = request.SecondaryPhone;
            client.Email = request.Email;
            client.PhysicalAddress = request.PhysicalAddress;
            client.City = request.City;
            client.Province = request.Province;
            client.Ministry = request.Ministry;
            client.EmployerType = request.EmployerType;
            client.EmploymentStatus = request.EmploymentStatus;
            client.UpdatedAt = DateTime.UtcNow;
            client.UpdatedBy = userId;
            client.VersionNumber++; // Increment version number

            await _context.SaveChangesAsync();

            // Create new version snapshot
            var versionResult = await _versioningService.CreateVersionAsync(
                client,
                "Client profile updated",
                userId,
                null, // IP address - could be added later
                null  // Correlation ID - could be added later
            );

            if (versionResult.IsFailure)
            {
                await transaction.RollbackAsync();
                return Result<ClientResponse>.Failure(versionResult.Error);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Updated client {ClientId} to version {VersionNumber} by user {UserId}", 
                client.Id, client.VersionNumber, userId);

            // Fire-and-forget audit event for update
            _ = _auditService.LogAuditEventAsync(
                action: "ClientUpdated",
                entityType: "Client",
                entityId: client.Id.ToString(),
                actor: userId,
                eventData: new { versionNumber = client.VersionNumber }
            );

            return Result<ClientResponse>.Success(MapToResponse(client));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error updating client {ClientId}", id);
            return Result<ClientResponse>.Failure($"Error updating client: {ex.Message}");
        }
    }

    private static ClientResponse MapToResponse(Client client)
    {
        return new ClientResponse
        {
            Id = client.Id,
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
            CreatedAt = client.CreatedAt,
            CreatedBy = client.CreatedBy,
            UpdatedAt = client.UpdatedAt,
            UpdatedBy = client.UpdatedBy,
            VersionNumber = client.VersionNumber
        };
    }
}
