using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class IncidentResponseService : IIncidentResponseService
{
    private readonly AdminDbContext _dbContext;
    private readonly IAlertmanagerClient _alertmanagerClient;
    private readonly ICamundaWorkflowService _camundaWorkflowService;
    private readonly IAuditService _auditService;
    private readonly IOptionsMonitor<IncidentResponseOptions> _optionsMonitor;
    private readonly ILogger<IncidentResponseService> _logger;

    public IncidentResponseService(
        AdminDbContext dbContext,
        IAlertmanagerClient alertmanagerClient,
        ICamundaWorkflowService camundaWorkflowService,
        IAuditService auditService,
        IOptionsMonitor<IncidentResponseOptions> optionsMonitor,
        ILogger<IncidentResponseService> logger)
    {
        _dbContext = dbContext;
        _alertmanagerClient = alertmanagerClient;
        _camundaWorkflowService = camundaWorkflowService;
        _auditService = auditService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<IncidentPlaybookDto>> GetPlaybooksAsync(CancellationToken cancellationToken)
    {
        var playbooks = await _dbContext.IncidentPlaybooks
            .AsNoTracking()
            .OrderBy(p => p.AlertName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (playbooks.Count == 0)
        {
            return Array.Empty<IncidentPlaybookDto>();
        }

        var playbookIds = playbooks.Select(p => p.Id).ToArray();
        var runs = await _dbContext.IncidentPlaybookRuns
            .AsNoTracking()
            .Where(r => playbookIds.Contains(r.IncidentPlaybookId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var runLookup = runs
            .GroupBy(r => r.IncidentPlaybookId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<IncidentPlaybookRunDto>)g.Take(5).Select(ToDto).ToList());

        return playbooks
            .Select(p => ToDto(p, runLookup.TryGetValue(p.Id, out var value) ? value : Array.Empty<IncidentPlaybookRunDto>()))
            .ToList();
    }

    public async Task<IncidentPlaybookDto?> GetPlaybookAsync(Guid playbookId, CancellationToken cancellationToken)
    {
        var playbook = await _dbContext.IncidentPlaybooks
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlaybookId == playbookId, cancellationToken)
            .ConfigureAwait(false);

        if (playbook is null)
        {
            return null;
        }

        var runs = await _dbContext.IncidentPlaybookRuns
            .AsNoTracking()
            .Where(r => r.IncidentPlaybookId == playbook.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(ToDto)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToDto(playbook, runs);
    }

    public async Task<IncidentPlaybookDto> CreatePlaybookAsync(CreateIncidentPlaybookRequest request, string createdBy, CancellationToken cancellationToken)
    {
        var playbook = new IncidentPlaybook
        {
            AlertName = request.AlertName.Trim(),
            Severity = request.Severity.Trim(),
            Title = request.Title.Trim(),
            Summary = request.Summary?.Trim(),
            DiagnosisSteps = request.DiagnosisSteps.Trim(),
            ResolutionSteps = request.ResolutionSteps.Trim(),
            EscalationPath = request.EscalationPath.Trim(),
            LinkedRunbookUrl = request.LinkedRunbookUrl?.Trim(),
            Owner = request.Owner.Trim(),
            AutomationProcessKey = request.AutomationProcessKey?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.IncidentPlaybooks.Add(playbook);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(createdBy, "PLAYBOOK_CREATED", "IncidentPlaybook", playbook.PlaybookId.ToString(), new
        {
            request.AlertName,
            request.Severity,
            request.Owner
        }, cancellationToken).ConfigureAwait(false);

        return ToDto(playbook, Array.Empty<IncidentPlaybookRunDto>());
    }

    public async Task<IncidentPlaybookDto?> UpdatePlaybookAsync(Guid playbookId, UpdateIncidentPlaybookRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        var playbook = await _dbContext.IncidentPlaybooks
            .FirstOrDefaultAsync(p => p.PlaybookId == playbookId, cancellationToken)
            .ConfigureAwait(false);

        if (playbook is null)
        {
            return null;
        }

        playbook.Title = request.Title.Trim();
        playbook.Summary = request.Summary?.Trim();
        playbook.DiagnosisSteps = request.DiagnosisSteps.Trim();
        playbook.ResolutionSteps = request.ResolutionSteps.Trim();
        playbook.EscalationPath = request.EscalationPath.Trim();
        playbook.LinkedRunbookUrl = request.LinkedRunbookUrl?.Trim();
        playbook.Owner = request.Owner.Trim();
        playbook.AutomationProcessKey = request.AutomationProcessKey?.Trim();
        playbook.IsActive = request.IsActive;
        playbook.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(updatedBy, "PLAYBOOK_UPDATED", "IncidentPlaybook", playbook.PlaybookId.ToString(), new
        {
            request.Title,
            request.Owner,
            request.IsActive
        }, cancellationToken).ConfigureAwait(false);

        var runs = await _dbContext.IncidentPlaybookRuns
            .AsNoTracking()
            .Where(r => r.IncidentPlaybookId == playbook.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(ToDto)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToDto(playbook, runs);
    }

    public async Task<IncidentPlaybookRunDto> RecordPlaybookRunAsync(Guid playbookId, RecordPlaybookUsageRequest request, string recordedBy, CancellationToken cancellationToken)
    {
        var playbook = await _dbContext.IncidentPlaybooks
            .FirstOrDefaultAsync(p => p.PlaybookId == playbookId, cancellationToken)
            .ConfigureAwait(false) ?? throw new KeyNotFoundException($"Playbook {playbookId} not found");

        var startedAt = request.StartedAt.ToUniversalTime();
        var resolvedAt = request.ResolvedAt?.ToUniversalTime();
        double? resolutionMinutes = request.ResolutionMinutesOverride;
        if (!resolutionMinutes.HasValue && resolvedAt.HasValue)
        {
            resolutionMinutes = (resolvedAt.Value - startedAt).TotalMinutes;
        }

        var run = new IncidentPlaybookRun
        {
            IncidentPlaybookId = playbook.Id,
            IncidentId = request.IncidentId,
            AlertName = request.AlertName.Trim(),
            Severity = request.Severity.Trim(),
            StartedAt = startedAt,
            AcknowledgedAt = request.AcknowledgedAt?.ToUniversalTime(),
            ResolvedAt = resolvedAt,
            ResolutionSummary = request.ResolutionSummary?.Trim(),
            AutomationInvoked = request.AutomationInvoked,
            AutomationOutcome = request.AutomationOutcome?.Trim(),
            PagerDutyIncidentId = request.PagerDutyIncidentId?.Trim(),
            RecordedBy = recordedBy,
            ResolutionMinutes = resolutionMinutes,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.IncidentPlaybookRuns.Add(run);
        playbook.LastUsedAt = DateTime.UtcNow;
        playbook.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(recordedBy, "PLAYBOOK_RUN_RECORDED", "IncidentPlaybook", playbook.PlaybookId.ToString(), new
        {
            request.IncidentId,
            request.AlertName,
            request.AutomationInvoked
        }, cancellationToken).ConfigureAwait(false);

        return ToDto(run);
    }

    public async Task<IReadOnlyCollection<AlertSilenceDto>> GetSilencesAsync(CancellationToken cancellationToken)
    {
        var silences = await _alertmanagerClient.GetSilencesAsync(cancellationToken).ConfigureAwait(false);
        return silences.Select(ToDto).ToList();
    }

    public async Task<AlertSilenceDto> CreateSilenceAsync(CreateAlertSilenceRequest request, string createdBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            request.CreatedBy = createdBy;
        }

        var silence = await _alertmanagerClient.CreateSilenceAsync(request, cancellationToken).ConfigureAwait(false);

        var auditRecord = new AlertSilenceAudit
        {
            SilenceId = silence.Id,
            CreatedBy = silence.CreatedBy,
            Comment = silence.Comment,
            CreatedAt = DateTime.UtcNow,
            StartsAt = silence.StartsAt,
            EndsAt = silence.EndsAt,
            Matchers = JsonSerializer.Serialize(silence.Matchers),
            AlertmanagerUrl = _optionsMonitor.CurrentValue.AlertmanagerBaseUrl
        };

        _dbContext.AlertSilenceAudits.Add(auditRecord);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(createdBy, "ALERT_SILENCED", "AlertmanagerSilence", silence.Id, new
        {
            silence.StartsAt,
            silence.EndsAt,
            silence.Matchers
        }, cancellationToken).ConfigureAwait(false);

        return ToDto(silence);
    }

    public async Task<IReadOnlyCollection<OperationalIncidentDto>> GetIncidentsAsync(CancellationToken cancellationToken)
    {
        var incidents = await _dbContext.OperationalIncidents
            .AsNoTracking()
            .Include(i => i.Playbook)
            .OrderByDescending(i => i.DetectedAt)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return incidents.Select(ToDto).ToList();
    }

    public async Task<OperationalIncidentDto> CreateIncidentAsync(CreateOperationalIncidentRequest request, string createdBy, CancellationToken cancellationToken)
    {
        IncidentPlaybook? playbook = null;
        if (request.PlaybookId.HasValue)
        {
            playbook = await _dbContext.IncidentPlaybooks
                .FirstOrDefaultAsync(p => p.PlaybookId == request.PlaybookId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        var incident = new OperationalIncident
        {
            AlertName = request.AlertName.Trim(),
            Severity = request.Severity.Trim(),
            Summary = request.Summary?.Trim(),
            Details = request.Details?.Trim(),
            IncidentPlaybookId = playbook?.Id,
            PagerDutyIncidentId = request.PagerDutyIncidentId?.Trim(),
            SlackThreadUrl = request.SlackThreadUrl?.Trim(),
            DetectedAt = (request.DetectedAt ?? DateTime.UtcNow).ToUniversalTime(),
            CreatedBy = createdBy,
            UpdatedAt = DateTime.UtcNow,
            Status = "Open",
            AutomationStatus = "Pending"
        };

        _dbContext.OperationalIncidents.Add(incident);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var processId = await _camundaWorkflowService.StartIncidentWorkflowAsync(incident, cancellationToken).ConfigureAwait(false);
        incident.CamundaProcessInstanceId = processId;
        incident.AutomationStatus = string.IsNullOrWhiteSpace(processId) ? "Manual" : "Automating";
        incident.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(createdBy, "INCIDENT_CREATED", "OperationalIncident", incident.IncidentId.ToString(), new
        {
            incident.AlertName,
            incident.Severity,
            incident.PagerDutyIncidentId,
            Playbook = playbook?.PlaybookId
        }, cancellationToken).ConfigureAwait(false);

        return ToDto(incident, playbook);
    }

    public async Task<OperationalIncidentDto?> ResolveIncidentAsync(Guid incidentId, ResolveOperationalIncidentRequest request, string resolvedBy, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.OperationalIncidents
            .Include(i => i.Playbook)
            .FirstOrDefaultAsync(i => i.IncidentId == incidentId, cancellationToken)
            .ConfigureAwait(false);

        if (incident is null)
        {
            return null;
        }

        incident.Status = "Resolved";
        incident.ResolvedAt = (request.ResolvedAt ?? DateTime.UtcNow).ToUniversalTime();
        incident.AutomationStatus = request.AutomationStatus?.Trim() ?? incident.AutomationStatus;
        incident.PostmortemDueAt = request.PostmortemDueAt?.ToUniversalTime();
        incident.PostmortemSummary = request.ResolutionSummary?.Trim();
        incident.LastUpdatedBy = resolvedBy;
        incident.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (request.TriggerPostmortemWorkflow && incident.PostmortemDueAt.HasValue)
        {
            await _camundaWorkflowService.StartPostIncidentReviewAsync(incident, incident.PostmortemDueAt.Value, cancellationToken).ConfigureAwait(false);
        }

        await LogAuditAsync(resolvedBy, "INCIDENT_RESOLVED", "OperationalIncident", incident.IncidentId.ToString(), new
        {
            incident.Status,
            incident.PostmortemDueAt,
            request.AutomationStatus
        }, cancellationToken).ConfigureAwait(false);

        return ToDto(incident, incident.Playbook);
    }

    private IncidentPlaybookDto ToDto(IncidentPlaybook playbook, IReadOnlyCollection<IncidentPlaybookRunDto> runs)
        => new()
        {
            PlaybookId = playbook.PlaybookId,
            AlertName = playbook.AlertName,
            Severity = playbook.Severity,
            Title = playbook.Title,
            Summary = playbook.Summary,
            DiagnosisSteps = playbook.DiagnosisSteps,
            ResolutionSteps = playbook.ResolutionSteps,
            EscalationPath = playbook.EscalationPath,
            LinkedRunbookUrl = ResolveRunbookUrl(playbook),
            Owner = playbook.Owner,
            IsActive = playbook.IsActive,
            AutomationProcessKey = playbook.AutomationProcessKey,
            CreatedAt = playbook.CreatedAt,
            UpdatedAt = playbook.UpdatedAt,
            LastUsedAt = playbook.LastUsedAt,
            RecentRuns = runs
        };

    private static IncidentPlaybookRunDto ToDto(IncidentPlaybookRun run) => new()
    {
        RunId = run.RunId,
        IncidentId = run.IncidentId,
        AlertName = run.AlertName,
        Severity = run.Severity,
        StartedAt = run.StartedAt,
        AcknowledgedAt = run.AcknowledgedAt,
        ResolvedAt = run.ResolvedAt,
        ResolutionMinutes = run.ResolutionMinutes,
        AutomationInvoked = run.AutomationInvoked,
        AutomationOutcome = run.AutomationOutcome,
        ResolutionSummary = run.ResolutionSummary,
        RecordedBy = run.RecordedBy,
        CamundaProcessInstanceId = run.CamundaProcessInstanceId,
        PagerDutyIncidentId = run.PagerDutyIncidentId
    };

    private OperationalIncidentDto ToDto(OperationalIncident incident, IncidentPlaybook? playbook = null)
        => new()
        {
            IncidentId = incident.IncidentId,
            AlertName = incident.AlertName,
            Severity = incident.Severity,
            Status = incident.Status,
            Summary = incident.Summary,
            Details = incident.Details,
            PagerDutyIncidentId = incident.PagerDutyIncidentId,
            SlackThreadUrl = incident.SlackThreadUrl,
            PlaybookId = playbook?.PlaybookId,
            PlaybookTitle = playbook?.Title,
            DetectedAt = incident.DetectedAt,
            AcknowledgedAt = incident.AcknowledgedAt,
            ResolvedAt = incident.ResolvedAt,
            PostmortemDueAt = incident.PostmortemDueAt,
            PostmortemCompletedAt = incident.PostmortemCompletedAt,
            AutomationStatus = incident.AutomationStatus,
            CamundaProcessInstanceId = incident.CamundaProcessInstanceId,
            UpdatedAt = incident.UpdatedAt
        };

    private AlertSilenceDto ToDto(AlertmanagerSilence silence)
        => new()
        {
            SilenceId = silence.Id,
            Comment = silence.Comment,
            CreatedBy = silence.CreatedBy,
            StartsAt = silence.StartsAt,
            EndsAt = silence.EndsAt,
            Matchers = silence.Matchers.Select(m => new AlertSilenceMatcherDto
            {
                Name = m.Name,
                Value = m.Value,
                IsRegex = m.IsRegex
            }).ToList()
        };

    private string? ResolveRunbookUrl(IncidentPlaybook playbook)
    {
        if (!string.IsNullOrWhiteSpace(playbook.LinkedRunbookUrl))
        {
            return playbook.LinkedRunbookUrl;
        }

        var baseUrl = _optionsMonitor.CurrentValue.PlaybookBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(playbook.AlertName.ToLowerInvariant())}";
    }

    private async Task LogAuditAsync(string actor, string action, string entityType, string entityId, object payload, CancellationToken cancellationToken)
    {
        var auditEvent = new AuditEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EventData = JsonSerializer.Serialize(payload)
        };

        await _auditService.LogEventAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }
}
