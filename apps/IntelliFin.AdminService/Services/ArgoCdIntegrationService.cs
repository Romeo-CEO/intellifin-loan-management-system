using System.Net.Http.Json;
using System.Text.Json;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.Services;

public interface IArgoCdIntegrationService
{
    Task<IReadOnlyList<ArgoCdApplicationDto>> GetApplicationsAsync(CancellationToken cancellationToken);
    Task<ArgoCdApplicationDto?> GetApplicationAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<ArgoCdRevisionDto>> GetApplicationHistoryAsync(string name, CancellationToken cancellationToken);
    Task TriggerSyncAsync(string name, ArgoCdSyncRequestParameters parameters, CancellationToken cancellationToken);
    Task TriggerRollbackAsync(string name, int revisionId, CancellationToken cancellationToken);
}

public record ArgoCdSyncRequestParameters(bool? Prune, bool? DryRun, int? RetryLimit);

internal sealed class ArgoCdIntegrationService : IArgoCdIntegrationService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ArgoCdIntegrationService> _logger;
    private readonly IOptionsMonitor<ArgoCdOptions> _optionsMonitor;
    private readonly IDisposable? _changeRegistration;

    public ArgoCdIntegrationService(
        HttpClient httpClient,
        IOptionsMonitor<ArgoCdOptions> optionsMonitor,
        ILogger<ArgoCdIntegrationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _optionsMonitor = optionsMonitor;

        _changeRegistration = _optionsMonitor.OnChange(ConfigureClient);
        ConfigureClient(_optionsMonitor.CurrentValue);
    }

    public async Task<IReadOnlyList<ArgoCdApplicationDto>> GetApplicationsAsync(CancellationToken cancellationToken)
    {
        await EnsureConfiguredAsync(cancellationToken);
        var response = await _httpClient.GetAsync("/api/v1/applications", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ArgoCdApplicationList>(SerializerOptions, cancellationToken)
            ?? new ArgoCdApplicationList(Array.Empty<ArgoCdApplicationItem>());

        var items = payload.Items ?? Array.Empty<ArgoCdApplicationItem>();
        return items.Select(MapApplication).ToArray();
    }

    public async Task<ArgoCdApplicationDto?> GetApplicationAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Application name is required", nameof(name));
        }

        await EnsureConfiguredAsync(cancellationToken);
        var response = await _httpClient.GetAsync($"/api/v1/applications/{Uri.EscapeDataString(name)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ArgoCdApplicationItem>(SerializerOptions, cancellationToken);
        return payload is null ? null : MapApplication(payload);
    }

    public async Task<IReadOnlyList<ArgoCdRevisionDto>> GetApplicationHistoryAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Application name is required", nameof(name));
        }

        await EnsureConfiguredAsync(cancellationToken);
        var response = await _httpClient.GetAsync($"/api/v1/applications/{Uri.EscapeDataString(name)}/history", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ArgoCdHistoryResponse>(SerializerOptions, cancellationToken)
                     ?? new ArgoCdHistoryResponse(Array.Empty<ArgoCdHistoryEntry>());

        var items = payload.Items ?? Array.Empty<ArgoCdHistoryEntry>();

        return items.Select(entry =>
            new ArgoCdRevisionDto(
                entry.Id,
                entry.Revision ?? string.Empty,
                entry.Author ?? string.Empty,
                entry.Message ?? string.Empty,
                entry.DeployedAt ?? DateTimeOffset.UtcNow,
                entry.Status ?? string.Empty)).ToArray();
    }

    public async Task TriggerSyncAsync(string name, ArgoCdSyncRequestParameters parameters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Application name is required", nameof(name));
        }

        await EnsureConfiguredAsync(cancellationToken);

        var payload = new
        {
            prune = parameters.Prune ?? false,
            dryRun = parameters.DryRun ?? false,
            retry = parameters.RetryLimit.HasValue ? new { limit = parameters.RetryLimit.Value } : null
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/applications/{Uri.EscapeDataString(name)}/sync",
            payload,
            SerializerOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        _logger.LogInformation("ArgoCD sync triggered for application {Application}", name);
    }

    public async Task TriggerRollbackAsync(string name, int revisionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Application name is required", nameof(name));
        }

        if (revisionId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionId));
        }

        await EnsureConfiguredAsync(cancellationToken);

        var payload = new { id = revisionId };
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/applications/{Uri.EscapeDataString(name)}/rollback",
            payload,
            SerializerOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        _logger.LogWarning("ArgoCD rollback triggered for application {Application} to revision {Revision}", name, revisionId);
    }

    private void ConfigureClient(ArgoCdOptions options)
    {
        if (options is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            _httpClient.BaseAddress = new Uri(options.Url, UriKind.Absolute);
        }

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Token);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 600));
    }

    private async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.IsConfigured)
        {
            _logger.LogWarning("ArgoCD integration requested but configuration missing");
            throw new InvalidOperationException("ArgoCD is not configured. Set ArgoCD:Url and ArgoCD:Token in configuration.");
        }

        await Task.CompletedTask;
    }

    private static ArgoCdApplicationDto MapApplication(ArgoCdApplicationItem application)
    {
        var status = application.Status ?? new ArgoCdApplicationStatus();
        return new ArgoCdApplicationDto(
            application.Metadata?.Name ?? string.Empty,
            application.Metadata?.Namespace ?? string.Empty,
            application.Spec?.Project ?? string.Empty,
            status.Sync?.Status ?? string.Empty,
            status.Health?.Status ?? string.Empty,
            status.Sync?.Revision ?? string.Empty,
            status.OperationState?.FinishedAt,
            application.Spec?.Destination?.Server ?? string.Empty);
    }

    public void Dispose()
    {
        _changeRegistration?.Dispose();
    }

    private sealed record ArgoCdApplicationList(IReadOnlyList<ArgoCdApplicationItem> Items);

    private sealed record ArgoCdApplicationItem
    {
        public ArgoCdMetadata? Metadata { get; init; }
        public ArgoCdApplicationSpec? Spec { get; init; }
        public ArgoCdApplicationStatus? Status { get; init; }
    }

    private sealed record ArgoCdMetadata
    {
        public string? Name { get; init; }
        public string? Namespace { get; init; }
    }

    private sealed record ArgoCdApplicationSpec
    {
        public string? Project { get; init; }
        public ArgoCdDestination? Destination { get; init; }
    }

    private sealed record ArgoCdDestination
    {
        public string? Server { get; init; }
    }

    private sealed record ArgoCdApplicationStatus
    {
        public ArgoCdSyncStatus? Sync { get; init; }
        public ArgoCdHealthStatus? Health { get; init; }
        public ArgoCdOperationState? OperationState { get; init; }
    }

    private sealed record ArgoCdSyncStatus
    {
        public string? Status { get; init; }
        public string? Revision { get; init; }
    }

    private sealed record ArgoCdHealthStatus
    {
        public string? Status { get; init; }
    }

    private sealed record ArgoCdOperationState
    {
        public DateTimeOffset? FinishedAt { get; init; }
    }

    private sealed record ArgoCdHistoryResponse(IReadOnlyList<ArgoCdHistoryEntry> Items);

    private sealed record ArgoCdHistoryEntry
    {
        public int Id { get; init; }
        public string? Revision { get; init; }
        public string? Author { get; init; }
        public string? Message { get; init; }
        public DateTimeOffset? DeployedAt { get; init; }
        public string? Status { get; init; }
    }
}
