using System.Text.Json;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class SodExceptionService : ISodExceptionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminDbContext _dbContext;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IAuditService _auditService;
    private readonly ILogger<SodExceptionService> _logger;

    public SodExceptionService(
        AdminDbContext dbContext,
        IKeycloakAdminService keycloakAdminService,
        IAuditService auditService,
        ILogger<SodExceptionService> logger)
    {
        _dbContext = dbContext;
        _keycloakAdminService = keycloakAdminService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<SodExceptionResponse> RequestExceptionAsync(
        SodExceptionRequest request,
        string requestorId,
        string requestorName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestorId);

        var now = DateTime.UtcNow;
        var exceptionId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString("N");

        var entity = new SodException
        {
            ExceptionId = exceptionId,
            UserId = request.UserId,
            UserName = request.UserName,
            RequestedRole = request.RequestedRole,
            ConflictingRolesJson = JsonSerializer.Serialize(request.ConflictingRoles, SerializerOptions),
            BusinessJustification = request.BusinessJustification,
            ExceptionDuration = request.ExceptionDuration,
            Status = "Pending",
            RequestedBy = requestorId,
            RequestedAt = now,
            CorrelationId = correlationId
        };

        _dbContext.SodExceptions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = now,
            Actor = requestorId,
            Action = "SodExceptionRequested",
            EntityType = nameof(SodException),
            EntityId = exceptionId.ToString(),
            CorrelationId = correlationId,
            EventData = JsonSerializer.Serialize(new
            {
                request.UserId,
                request.RequestedRole,
                request.ConflictingRoles,
                request.ExceptionDuration
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "SoD exception requested for user {User} role {Role} by {Requestor}",
            request.UserId,
            request.RequestedRole,
            requestorId);

        return new SodExceptionResponse(
            exceptionId,
            "Pending",
            "SoD exception request submitted for review.",
            now.AddHours(24));
    }

    public async Task<SodExceptionStatusDto?> GetExceptionStatusAsync(Guid exceptionId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SodExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExceptionId == exceptionId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var roles = DeserializeRoles(entity.ConflictingRolesJson);

        return new SodExceptionStatusDto(
            entity.ExceptionId,
            entity.Status,
            entity.RequestedRole,
            roles,
            entity.BusinessJustification,
            entity.RequestedAt,
            entity.RequestedBy,
            entity.ApprovedAt,
            entity.ExpiresAt,
            entity.ReviewedBy,
            entity.ReviewComments);
    }

    public async Task ApproveExceptionAsync(
        Guid exceptionId,
        string reviewerId,
        string reviewerName,
        string comments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);

        var entity = await _dbContext.SodExceptions
            .FirstOrDefaultAsync(e => e.ExceptionId == exceptionId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("SoD exception not found.");
        }

        if (!string.Equals(entity.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Exception is not pending. Current status: {entity.Status}");
        }

        var now = DateTime.UtcNow;
        entity.Status = "Active";
        entity.ReviewedBy = reviewerId;
        entity.ReviewedAt = now;
        entity.ReviewComments = comments;
        entity.ApprovedAt = now;
        entity.ExpiresAt = now.AddDays(entity.ExceptionDuration);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _keycloakAdminService.AssignRolesAsync(
            entity.UserId,
            new AssignRolesRequest { Roles = new[] { entity.RequestedRole } },
            cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = now,
            Actor = reviewerId,
            Action = "SodExceptionApproved",
            EntityType = nameof(SodException),
            EntityId = exceptionId.ToString(),
            CorrelationId = entity.CorrelationId,
            EventData = JsonSerializer.Serialize(new
            {
                entity.UserId,
                entity.RequestedRole,
                entity.ExpiresAt,
                comments
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "SoD exception {ExceptionId} approved by {Reviewer}",
            exceptionId,
            reviewerId);
    }

    public async Task RejectExceptionAsync(
        Guid exceptionId,
        string reviewerId,
        string reviewerName,
        string comments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);

        var entity = await _dbContext.SodExceptions
            .FirstOrDefaultAsync(e => e.ExceptionId == exceptionId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("SoD exception not found.");
        }

        if (!string.Equals(entity.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Exception is not pending. Current status: {entity.Status}");
        }

        var now = DateTime.UtcNow;
        entity.Status = "Rejected";
        entity.ReviewedBy = reviewerId;
        entity.ReviewedAt = now;
        entity.ReviewComments = comments;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = now,
            Actor = reviewerId,
            Action = "SodExceptionRejected",
            EntityType = nameof(SodException),
            EntityId = exceptionId.ToString(),
            CorrelationId = entity.CorrelationId,
            EventData = JsonSerializer.Serialize(new
            {
                entity.UserId,
                entity.RequestedRole,
                comments
            }, SerializerOptions)
        }, cancellationToken);

        _logger.LogInformation(
            "SoD exception {ExceptionId} rejected by {Reviewer}",
            exceptionId,
            reviewerId);
    }

    public async Task<SodComplianceReportDto> GenerateComplianceReportAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var activeCount = await _dbContext.SodExceptions
            .AsNoTracking()
            .Where(e => e.Status == "Active" && e.ExpiresAt > now)
            .CountAsync(cancellationToken);

        var expiredCount = await _dbContext.SodExceptions
            .AsNoTracking()
            .Where(e => e.Status == "Expired" && e.ExpiredAt >= startDate && e.ExpiredAt <= endDate)
            .CountAsync(cancellationToken);

        var blockedAssignments = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(e => e.Action == "SodConflictDetected" && e.Timestamp >= startDate && e.Timestamp <= endDate)
            .CountAsync(cancellationToken);

        var activeExceptions = await _dbContext.SodExceptions
            .AsNoTracking()
            .Where(e => e.Status == "Active" && e.ExpiresAt > now)
            .Select(e => new SodExceptionSummaryDto(
                e.ExceptionId,
                e.RequestedRole,
                e.ExpiresAt ?? now,
                e.BusinessJustification,
                e.ReviewedBy))
            .ToListAsync(cancellationToken);

        return new SodComplianceReportDto(
            startDate,
            endDate,
            activeCount,
            expiredCount,
            blockedAssignments,
            activeExceptions,
            now);
    }

    public async Task<IReadOnlyCollection<SodPolicyDto>> GetAllPoliciesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.SodPolicies
            .AsNoTracking()
            .OrderBy(p => p.Severity)
            .ThenBy(p => p.Role1)
            .Select(p => new SodPolicyDto(p.Id, p.Role1, p.Role2, p.ConflictDescription, p.Severity, p.Enabled))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdatePolicyAsync(int policyId, SodPolicyUpdateDto update, string adminId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminId);

        var policy = await _dbContext.SodPolicies.FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken);
        if (policy is null)
        {
            throw new InvalidOperationException("SoD policy not found.");
        }

        policy.Enabled = update.Enabled;
        policy.Severity = update.Severity;
        if (!string.IsNullOrWhiteSpace(update.ConflictDescription))
        {
            policy.ConflictDescription = update.ConflictDescription;
        }

        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = adminId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogEventAsync(new AuditEvent
        {
            Timestamp = DateTime.UtcNow,
            Actor = adminId,
            Action = "SodPolicyUpdated",
            EntityType = nameof(SodPolicy),
            EntityId = policyId.ToString(),
            EventData = JsonSerializer.Serialize(new
            {
                policy.Role1,
                policy.Role2,
                update.Enabled,
                update.Severity,
                update.ConflictDescription
            }, SerializerOptions)
        }, cancellationToken);
    }

    private static IReadOnlyCollection<string> DeserializeRoles(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var roles = JsonSerializer.Deserialize<IReadOnlyCollection<string>>(json, SerializerOptions);
            return roles ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

