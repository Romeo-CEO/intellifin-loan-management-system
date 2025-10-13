using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.Camunda;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace IntelliFin.AdminService.Services;

public sealed class BastionAccessService : IBastionAccessService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlySet<string> AllowedEnvironments =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dev", "staging", "production" };

    private readonly AdminDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ICamundaClient _camundaClient;
    private readonly IMinioClient _minioClient;
    private readonly IOptionsMonitor<BastionOptions> _bastionOptions;
    private readonly ILogger<BastionAccessService> _logger;

    public BastionAccessService(
        AdminDbContext dbContext,
        IAuditService auditService,
        ICamundaClient camundaClient,
        IMinioClient minioClient,
        IOptionsMonitor<BastionOptions> bastionOptions,
        ILogger<BastionAccessService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _camundaClient = camundaClient;
        _minioClient = minioClient;
        _bastionOptions = bastionOptions;
        _logger = logger;
    }

    public async Task<BastionAccessRequestDto> RequestAccessAsync(
        BastionAccessRequestInput request,
        string userId,
        string userName,
        string userEmail,
        CancellationToken cancellationToken)
    {
        if (!AllowedEnvironments.Contains(request.Environment))
        {
            throw new ValidationException("Unsupported environment requested for bastion access.");
        }

        var options = _bastionOptions.CurrentValue;
        var normalizedEnvironment = request.Environment.ToLowerInvariant();
        var durationHours = Math.Clamp(request.AccessDurationHours, options.MinAccessDurationHours, options.MaxAccessDurationHours);
        var requiresApproval = string.Equals(normalizedEnvironment, "production", StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        var entity = new BastionAccessRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            UserEmail = userEmail,
            Environment = normalizedEnvironment,
            AccessDurationHours = durationHours,
            Justification = request.Justification,
            RequiresApproval = requiresApproval,
            Status = requiresApproval ? "Pending" : "Approved",
            RequestedAt = now,
            ApprovedAt = requiresApproval ? null : now,
            ApprovedBy = requiresApproval ? null : "SYSTEM",
            VaultCertificatePath = requiresApproval
                ? null
                : ResolveVaultRole(normalizedEnvironment),
            TargetHosts = request.TargetHosts is { Count: > 0 }
                ? JsonSerializer.Serialize(request.TargetHosts, JsonOptions)
                : null
        };

        if (!requiresApproval)
        {
            entity.SshCertificateIssued = true;
            entity.CertificateExpiresAt = now.AddHours(durationHours);
            entity.CertificateSerialNumber = $"AUTO-{Guid.NewGuid():N}".ToUpperInvariant();
            entity.CertificateContent = GeneratePlaceholderCertificate(userName, entity.CertificateExpiresAt.Value);
        }

        try
        {
            entity.CamundaProcessInstanceId = await _camundaClient.StartProcessAsync(
                options.AccessWorkflowProcessId,
                new Dictionary<string, object>
                {
                    ["requestId"] = entity.RequestId.ToString(),
                    ["userId"] = userId,
                    ["userName"] = userName,
                    ["environment"] = normalizedEnvironment,
                    ["requiresApproval"] = requiresApproval,
                    ["durationHours"] = durationHours,
                    ["justification"] = request.Justification
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start bastion access Camunda workflow for request {RequestId}", entity.RequestId);
        }

        _dbContext.BastionAccessRequests.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEvent
        {
            Actor = userId,
            Action = "BastionAccessRequested",
            EntityType = nameof(BastionAccessRequest),
            EntityId = entity.RequestId.ToString(),
            CorrelationId = entity.RequestId.ToString(),
            EventData = JsonSerializer.Serialize(new
            {
                environment = normalizedEnvironment,
                durationHours,
                requiresApproval,
                request.TargetHosts
            }, JsonOptions)
        }, cancellationToken);

        return new BastionAccessRequestDto(
            entity.RequestId,
            entity.Status,
            entity.RequiresApproval,
            entity.RequestedAt,
            entity.ApprovedAt,
            entity.CertificateExpiresAt,
            entity.Environment,
            entity.AccessDurationHours,
            DeserializeTargets(entity.TargetHosts),
            entity.CertificateContent,
            options.BastionHostname,
            BuildConnectionInstructions(options.BastionHostname, entity.RequestId));
    }

    public async Task<BastionAccessRequestStatusDto?> GetAccessRequestStatusAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.BastionAccessRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

        return request is null
            ? null
            : new BastionAccessRequestStatusDto(
                request.RequestId,
                request.Status,
                request.RequiresApproval,
                request.RequestedAt,
                request.ApprovedAt,
                request.CertificateExpiresAt,
                request.Environment);
    }

    public async Task<BastionCertificateDto?> GetSshCertificateAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.BastionAccessRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, cancellationToken);

        if (request is null || !request.SshCertificateIssued || string.IsNullOrWhiteSpace(request.CertificateContent))
        {
            return null;
        }

        if (request.CertificateExpiresAt is { } expiresAt && expiresAt < DateTime.UtcNow)
        {
            return null;
        }

        return new BastionCertificateDto(
            request.RequestId,
            request.CertificateContent!,
            request.CertificateExpiresAt ?? DateTime.UtcNow,
            _bastionOptions.CurrentValue.BastionHostname);
    }

    public async Task<IReadOnlyCollection<BastionSessionDto>> GetActiveSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.BastionSessions
            .AsNoTracking()
            .Where(s => s.Status == "Active")
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(s => new BastionSessionDto(
                s.SessionId,
                s.Username,
                s.ClientIp,
                s.BastionHost,
                s.TargetHost,
                s.StartTime,
                s.EndTime,
                s.DurationSeconds,
                s.Status,
                s.CommandCount))
            .ToList();
    }

    public async Task<SessionRecordingDto?> GetSessionRecordingAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid))
        {
            return null;
        }

        var session = await _dbContext.BastionSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionGuid, cancellationToken);

        if (session is null || string.IsNullOrWhiteSpace(session.RecordingPath))
        {
            return null;
        }

        var options = _bastionOptions.CurrentValue;
        var objectKey = CombinePath(options.SessionPrefix, session.RecordingPath);
        string? downloadUrl = null;

        try
        {
            downloadUrl = await _minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(options.SessionBucketName)
                .WithObject(objectKey)
                .WithExpiry((int)TimeSpan.FromHours(1).TotalSeconds), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate presigned URL for bastion session {SessionId}", session.SessionId);
        }

        return new SessionRecordingDto(
            session.SessionId,
            session.RecordingPath!,
            downloadUrl,
            session.StartTime,
            session.EndTime,
            session.BastionHost,
            session.TargetHost,
            session.Username);
    }

    public async Task<EmergencyAccessDto> RequestEmergencyAccessAsync(
        EmergencyAccessRequest request,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.ApproverOneId, request.ApproverTwoId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Approvers must be distinct users.");
        }

        var options = _bastionOptions.CurrentValue;
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(Math.Max(1, options.EmergencyAccessHours));
        var emergency = new EmergencyAccessLog
        {
            EmergencyId = Guid.NewGuid(),
            RequestedBy = requestedBy,
            ApprovedBy1 = request.ApproverOneId,
            ApprovedBy2 = request.ApproverTwoId,
            IncidentTicketId = request.IncidentTicketId,
            Justification = request.Justification,
            RequestedAt = now,
            GrantedAt = now,
            ExpiresAt = expiresAt,
            VaultOneTimeToken = GenerateEmergencyToken(),
            ReviewNotes = null
        };

        _dbContext.EmergencyAccessLogs.Add(emergency);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _camundaClient.StartProcessAsync(
                options.EmergencyWorkflowProcessId,
                new Dictionary<string, object>
                {
                    ["emergencyId"] = emergency.EmergencyId.ToString(),
                    ["incidentTicketId"] = emergency.IncidentTicketId,
                    ["requestedBy"] = requestedBy,
                    ["approverOne"] = request.ApproverOneId,
                    ["approverTwo"] = request.ApproverTwoId
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start emergency bastion workflow for incident {Incident}", request.IncidentTicketId);
        }

        await _auditService.LogAsync(new AuditEvent
        {
            Actor = requestedBy,
            Action = "BastionEmergencyAccess",
            EntityType = nameof(EmergencyAccessLog),
            EntityId = emergency.EmergencyId.ToString(),
            CorrelationId = emergency.IncidentTicketId,
            Severity = "Critical",
            EventData = JsonSerializer.Serialize(new
            {
                emergency.EmergencyId,
                emergency.IncidentTicketId,
                emergency.ExpiresAt,
                request.ApproverOneId,
                request.ApproverTwoId
            }, JsonOptions)
        }, cancellationToken);

        return new EmergencyAccessDto(
            emergency.EmergencyId,
            emergency.IncidentTicketId,
            emergency.GrantedAt,
            emergency.ExpiresAt,
            emergency.VaultOneTimeToken ?? string.Empty,
            requestedBy,
            emergency.ApprovedBy1,
            emergency.ApprovedBy2);
    }

    public async Task RecordSessionAsync(
        BastionSessionIngestRequest request,
        CancellationToken cancellationToken)
    {
        var sessionStart = request.StartTime ?? DateTime.UtcNow;
        var sessionEnd = request.EndTime;

        if (sessionEnd.HasValue && sessionEnd.Value < sessionStart)
        {
            sessionEnd = sessionStart;
        }

        var status = NormalizeStatus(request.Status);

        var session = await _dbContext.BastionSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
        {
            session = new BastionSession
            {
                SessionId = request.SessionId,
                Username = request.Username,
                ClientIp = request.ClientIp,
                BastionHost = request.BastionHost,
                TargetHost = NormalizeOptional(request.TargetHost),
                StartTime = sessionStart,
                EndTime = sessionEnd,
                DurationSeconds = CalculateDurationSeconds(sessionStart, sessionEnd),
                RecordingPath = request.RecordingPath,
                RecordingSize = request.RecordingSize,
                Status = status,
                CommandCount = request.CommandCount ?? 0,
                AccessRequestId = await ResolveAccessRequestIdAsync(request, sessionStart, cancellationToken)
            };

            _dbContext.BastionSessions.Add(session);
        }
        else
        {
            session.Username = request.Username;
            session.ClientIp = request.ClientIp;
            session.BastionHost = request.BastionHost;
            session.TargetHost = NormalizeOptional(request.TargetHost);
            session.StartTime = request.StartTime ?? session.StartTime;
            session.EndTime = sessionEnd ?? session.EndTime;
            session.DurationSeconds = CalculateDurationSeconds(session.StartTime, session.EndTime);
            session.RecordingPath = request.RecordingPath;
            session.RecordingSize = request.RecordingSize ?? session.RecordingSize;
            session.Status = status;
            session.CommandCount = request.CommandCount ?? session.CommandCount;

            var resolvedAccessRequestId = await ResolveAccessRequestIdAsync(request, session.StartTime, cancellationToken);
            if (resolvedAccessRequestId.HasValue)
            {
                session.AccessRequestId = resolvedAccessRequestId;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEvent
        {
            Actor = request.Username,
            Action = "BastionSessionRecorded",
            EntityType = nameof(BastionSession),
            EntityId = session.SessionId.ToString(),
            CorrelationId = session.AccessRequestId?.ToString(),
            EventData = JsonSerializer.Serialize(new
            {
                request.Username,
                request.ClientIp,
                request.BastionHost,
                request.RecordingPath,
                session.StartTime,
                session.EndTime,
                session.Status,
                session.AccessRequestId
            }, JsonOptions)
        }, cancellationToken);
    }

    private static string ResolveVaultRole(string environment) =>
        string.Equals(environment, "production", StringComparison.OrdinalIgnoreCase)
            ? "ssh/sign/admin-role"
            : "ssh/sign/developer-role";

    private static IReadOnlyCollection<string> DeserializeTargets(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return Array.Empty<string>();
        }

        try
        {
            var hosts = JsonSerializer.Deserialize<List<string>>(serialized, JsonOptions);
            return hosts is { Count: > 0 }
                ? hosts.AsReadOnly()
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string GeneratePlaceholderCertificate(string principal, DateTime expiresAt)
    {
        var expiryUnix = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        var random = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        return $"ssh-ed25519-cert-v01@openssh.com AAAAIHNzaC1lZDI1NTE5LWNlcnQtdjAxQG9wZW5zc2guY29tAAAAII{random[..20]} {principal} {expiryUnix}";
    }

    private static string BuildConnectionInstructions(string bastionHost, Guid requestId) =>
        $"ssh -o CertificateFile=bastion-cert-{requestId}.pub ubuntu@{bastionHost}";

    private static string CombinePath(string? prefix, string path)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return path;
        }
        var trimmedPrefix = prefix!.Trim('/');
        var normalizedPath = path.TrimStart('/');
        if (normalizedPath.StartsWith(trimmedPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        return string.Join('/', trimmedPrefix, normalizedPath);
    }

    private static string GenerateEmergencyToken() => $"EMERG-{Guid.NewGuid():N}";

    private static string NormalizeStatus(string? status)
        => string.IsNullOrWhiteSpace(status) ? "Completed" : status.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? CalculateDurationSeconds(DateTime start, DateTime? end)
    {
        if (end is null)
        {
            return null;
        }

        var duration = (int)Math.Round((end.Value - start).TotalSeconds);
        return Math.Max(0, duration);
    }

    private async Task<Guid?> ResolveAccessRequestIdAsync(
        BastionSessionIngestRequest request,
        DateTime sessionStart,
        CancellationToken cancellationToken)
    {
        if (request.AccessRequestId is { } explicitId)
        {
            var exists = await _dbContext.BastionAccessRequests
                .AsNoTracking()
                .AnyAsync(r => r.RequestId == explicitId, cancellationToken);

            if (exists)
            {
                return explicitId;
            }
        }

        var lookbackHours = Math.Max(1, _bastionOptions.CurrentValue.SessionAssociationLookbackHours);
        var windowStart = sessionStart.AddHours(-lookbackHours);

        var normalizedUser = request.Username.ToLowerInvariant();

        var candidate = await _dbContext.BastionAccessRequests
            .AsNoTracking()
            .Where(r => r.RequestedAt >= windowStart)
            .Where(r => r.UserId.ToLower() == normalizedUser || r.UserName.ToLower() == normalizedUser)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return candidate?.RequestId;
    }
}
