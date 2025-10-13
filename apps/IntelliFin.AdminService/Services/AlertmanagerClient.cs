using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public sealed class AlertmanagerClient : IAlertmanagerClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<IncidentResponseOptions> _optionsMonitor;
    private readonly ILogger<AlertmanagerClient> _logger;

    public AlertmanagerClient(
        HttpClient httpClient,
        IOptionsMonitor<IncidentResponseOptions> optionsMonitor,
        ILogger<AlertmanagerClient> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<AlertmanagerSilence>> GetSilencesAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/api/v2/silences", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Alertmanager returned {Status} when fetching silences", response.StatusCode);
            return Array.Empty<AlertmanagerSilence>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<List<AlertmanagerSilencePayload>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return Array.Empty<AlertmanagerSilence>();
        }

        return payload
            .Select(s => new AlertmanagerSilence
            {
                Id = s.Id ?? string.Empty,
                Comment = s.Comment,
                CreatedBy = s.CreatedBy ?? "unknown",
                StartsAt = s.StartsAt,
                EndsAt = s.EndsAt,
                Matchers = s.Matchers?.Select(m => new AlertmanagerMatcher(m.Name ?? string.Empty, m.Value ?? string.Empty, m.IsRegex)).ToList() ?? Array.Empty<AlertmanagerMatcher>()
            })
            .OrderByDescending(s => s.EndsAt)
            .ToList();
    }

    public async Task<AlertmanagerSilence> CreateSilenceAsync(CreateAlertSilenceRequest request, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var startsAt = (request.StartsAt ?? DateTime.UtcNow).ToUniversalTime();
        var endsAt = (request.EndsAt ?? startsAt.AddMinutes(Math.Max(5, options.DefaultSilenceDurationMinutes))).ToUniversalTime();
        var createdBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "admin-service" : request.CreatedBy!;
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? "Created from Admin Service" : request.Comment!;

        var payload = new AlertmanagerSilenceCreatePayload
        {
            Matchers = request.Matchers.Select(m => new AlertmanagerMatcherPayload
            {
                Name = m.Name,
                Value = m.Value,
                IsRegex = m.IsRegex
            }).ToList(),
            CreatedBy = createdBy,
            Comment = comment,
            StartsAt = startsAt,
            EndsAt = endsAt
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v2/silences", payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Failed to create alertmanager silence. Status: {Status}, Payload: {Payload}", response.StatusCode, body);
            throw new InvalidOperationException($"Alertmanager rejected silence creation with status {response.StatusCode}");
        }

        var content = await response.Content.ReadFromJsonAsync<SilenceCreateResponse>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        var silenceId = content?.SilenceId ?? string.Empty;

        return new AlertmanagerSilence
        {
            Id = silenceId,
            Comment = comment,
            CreatedBy = createdBy,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Matchers = payload.Matchers.Select(m => new AlertmanagerMatcher(m.Name, m.Value, m.IsRegex)).ToList()
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record AlertmanagerSilencePayload(string? Id, string? Comment, string? CreatedBy, DateTime StartsAt, DateTime EndsAt, List<AlertmanagerMatcherPayload>? Matchers);

    private sealed record AlertmanagerMatcherPayload
    {
        public string Name { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public bool IsRegex { get; init; }
    }

    private sealed record AlertmanagerSilenceCreatePayload
    {
        public List<AlertmanagerMatcherPayload> Matchers { get; init; } = new();
        public string CreatedBy { get; init; } = string.Empty;
        public string Comment { get; init; } = string.Empty;
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
    }

    private sealed record SilenceCreateResponse(string? SilenceId);
}
