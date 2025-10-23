using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service implementation for managing communication consent preferences
/// </summary>
public class ConsentManagementService : IConsentManagementService
{
    private readonly ClientManagementDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ConsentManagementService> _logger;

    public ConsentManagementService(
        ClientManagementDbContext context,
        IAuditService auditService,
        ILogger<ConsentManagementService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<ConsentResponse?>> GetConsentAsync(Guid clientId, string consentType)
    {
        try
        {
            var consent = await _context.CommunicationConsents
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.ConsentType == consentType);

            if (consent == null)
            {
                return Result<ConsentResponse?>.Success(null);
            }

            return Result<ConsentResponse?>.Success(MapToResponse(consent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consent for client {ClientId}, type {ConsentType}",
                clientId, consentType);
            return Result<ConsentResponse?>.Failure($"Error retrieving consent: {ex.Message}");
        }
    }

    public async Task<Result<List<ConsentResponse>>> GetAllConsentsAsync(Guid clientId)
    {
        try
        {
            // Verify client exists
            var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId);
            if (!clientExists)
            {
                _logger.LogWarning("Client not found: {ClientId}", clientId);
                return Result<List<ConsentResponse>>.Failure($"Client with ID {clientId} not found");
            }

            var consents = await _context.CommunicationConsents
                .Where(c => c.ClientId == clientId)
                .OrderBy(c => c.ConsentType)
                .ToListAsync();

            var response = consents.Select(MapToResponse).ToList();

            _logger.LogInformation("Retrieved {Count} consent records for client {ClientId}",
                response.Count, clientId);

            return Result<List<ConsentResponse>>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consents for client {ClientId}", clientId);
            return Result<List<ConsentResponse>>.Failure($"Error retrieving consents: {ex.Message}");
        }
    }

    public async Task<Result<ConsentResponse>> UpdateConsentAsync(
        Guid clientId,
        UpdateConsentRequest request,
        string userId,
        string? correlationId = null)
    {
        try
        {
            // Verify client exists
            var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId);
            if (!clientExists)
            {
                _logger.LogWarning("Client not found: {ClientId}", clientId);
                return Result<ConsentResponse>.Failure($"Client with ID {clientId} not found");
            }

            // Load or create consent record
            var consent = await _context.CommunicationConsents
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.ConsentType == request.ConsentType);

            var isNewConsent = consent == null;
            var now = DateTime.UtcNow;

            if (isNewConsent)
            {
                // Create new consent
                consent = new CommunicationConsent
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    ConsentType = request.ConsentType,
                    ConsentGivenAt = now,
                    ConsentGivenBy = userId,
                    CorrelationId = correlationId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.CommunicationConsents.Add(consent);
            }
            else
            {
                consent.UpdatedAt = now;
            }

            // Check if this is a revocation (all channels disabled)
            var isRevocation = !request.SmsEnabled
                && !request.EmailEnabled
                && !request.InAppEnabled
                && !request.CallEnabled;

            if (isRevocation)
            {
                // Mark consent as revoked
                consent.ConsentRevokedAt = now;
                consent.RevocationReason = request.RevocationReason;
                consent.SmsEnabled = false;
                consent.EmailEnabled = false;
                consent.InAppEnabled = false;
                consent.CallEnabled = false;

                _logger.LogInformation(
                    "Consent revoked for client {ClientId}, type {ConsentType}, reason: {Reason}",
                    clientId, request.ConsentType, request.RevocationReason);
            }
            else
            {
                // Update channel preferences
                consent.SmsEnabled = request.SmsEnabled;
                consent.EmailEnabled = request.EmailEnabled;
                consent.InAppEnabled = request.InAppEnabled;
                consent.CallEnabled = request.CallEnabled;

                // Clear revocation if re-granting consent
                if (consent.ConsentRevokedAt != null)
                {
                    consent.ConsentRevokedAt = null;
                    consent.RevocationReason = null;
                    consent.ConsentGivenAt = now;
                    consent.ConsentGivenBy = userId;

                    _logger.LogInformation(
                        "Consent re-granted for client {ClientId}, type {ConsentType}",
                        clientId, request.ConsentType);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Consent updated for client {ClientId}, type {ConsentType}: SMS={Sms}, Email={Email}, InApp={InApp}, Call={Call}",
                clientId, request.ConsentType, consent.SmsEnabled, consent.EmailEnabled,
                consent.InAppEnabled, consent.CallEnabled);

            // Log audit event (fire-and-forget)
            await _auditService.LogAuditEventAsync(
                action: "ConsentUpdated",
                entityType: "CommunicationConsent",
                entityId: consent.Id.ToString(),
                actor: userId,
                eventData: new
                {
                    ClientId = clientId,
                    ConsentType = request.ConsentType,
                    SmsEnabled = consent.SmsEnabled,
                    EmailEnabled = consent.EmailEnabled,
                    InAppEnabled = consent.InAppEnabled,
                    CallEnabled = consent.CallEnabled,
                    IsRevocation = isRevocation,
                    IsNew = isNewConsent
                });

            return Result<ConsentResponse>.Success(MapToResponse(consent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating consent for client {ClientId}", clientId);
            return Result<ConsentResponse>.Failure($"Error updating consent: {ex.Message}");
        }
    }

    public async Task<bool> CheckConsentAsync(Guid clientId, string consentType, string channel)
    {
        try
        {
            var consent = await _context.CommunicationConsents
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.ConsentType == consentType);

            // No consent record = no consent (default deny)
            if (consent == null)
            {
                _logger.LogDebug(
                    "No consent record found for client {ClientId}, type {ConsentType}",
                    clientId, consentType);
                return false;
            }

            // Consent revoked = no consent
            if (consent.ConsentRevokedAt != null)
            {
                _logger.LogDebug(
                    "Consent revoked for client {ClientId}, type {ConsentType}",
                    clientId, consentType);
                return false;
            }

            // Check specific channel
            var channelEnabled = consent.IsChannelEnabled(channel);

            _logger.LogDebug(
                "Consent check for client {ClientId}, type {ConsentType}, channel {Channel}: {Result}",
                clientId, consentType, channel, channelEnabled);

            return channelEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking consent for client {ClientId}, type {ConsentType}, channel {Channel}",
                clientId, consentType, channel);
            
            // On error, default to no consent (fail secure)
            return false;
        }
    }

    private static ConsentResponse MapToResponse(CommunicationConsent consent)
    {
        return new ConsentResponse
        {
            Id = consent.Id,
            ClientId = consent.ClientId,
            ConsentType = consent.ConsentType,
            SmsEnabled = consent.SmsEnabled,
            EmailEnabled = consent.EmailEnabled,
            InAppEnabled = consent.InAppEnabled,
            CallEnabled = consent.CallEnabled,
            ConsentGivenAt = consent.ConsentGivenAt,
            ConsentGivenBy = consent.ConsentGivenBy,
            ConsentRevokedAt = consent.ConsentRevokedAt,
            RevocationReason = consent.RevocationReason,
            IsActive = consent.IsActive
        };
    }
}
