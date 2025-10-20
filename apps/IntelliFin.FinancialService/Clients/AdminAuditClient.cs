using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using IntelliFin.FinancialService.Exceptions;
using IntelliFin.FinancialService.Models.Audit;
using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;

namespace IntelliFin.FinancialService.Clients;

public interface IAdminAuditClient
{
    Task<AuditEventPageResponse> GetEventsAsync(AuditEventQuery query, CancellationToken cancellationToken = default);
    Task<AuditExportResult> ExportEventsAsync(DateTime? startDate, DateTime? endDate, string format, CancellationToken cancellationToken = default);
    Task<AuditIntegrityStatusResponse> GetIntegrityStatusAsync(CancellationToken cancellationToken = default);
    Task<AuditIntegrityHistoryResponse> GetIntegrityHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public sealed class AdminAuditClient : IAdminAuditClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminAuditClient> _logger;

    public AdminAuditClient(HttpClient httpClient, ILogger<AdminAuditClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AuditEventPageResponse> GetEventsAsync(AuditEventQuery query, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            [nameof(AuditEventQuery.Page)] = query.Page.ToString(CultureInfo.InvariantCulture),
            [nameof(AuditEventQuery.PageSize)] = query.PageSize.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            parameters[nameof(AuditEventQuery.Actor)] = query.Actor;
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            parameters[nameof(AuditEventQuery.Action)] = query.Action;
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            parameters[nameof(AuditEventQuery.EntityType)] = query.EntityType;
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            parameters[nameof(AuditEventQuery.EntityId)] = query.EntityId;
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            parameters[nameof(AuditEventQuery.CorrelationId)] = query.CorrelationId;
        }

        if (query.StartDate.HasValue)
        {
            parameters[nameof(AuditEventQuery.StartDate)] = query.StartDate.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        if (query.EndDate.HasValue)
        {
            parameters[nameof(AuditEventQuery.EndDate)] = query.EndDate.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        var requestUri = QueryHelpers.AddQueryString("/api/admin/audit/events", parameters);
        _logger.LogDebug("Requesting audit events from {Endpoint}", requestUri);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, nameof(GetEventsAsync));

        var payload = await response.Content.ReadFromJsonAsync<AuditEventPageResponse>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("AdminService returned an empty audit event page.");
    }

    public async Task<AuditExportResult> ExportEventsAsync(DateTime? startDate, DateTime? endDate, string format, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            [nameof(format)] = format
        };

        if (startDate.HasValue)
        {
            parameters[nameof(startDate)] = startDate.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        if (endDate.HasValue)
        {
            parameters[nameof(endDate)] = endDate.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        var requestUri = QueryHelpers.AddQueryString("/api/admin/audit/events/export", parameters);
        _logger.LogDebug("Exporting audit events from {Endpoint}", requestUri);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, nameof(ExportEventsAsync));

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/csv";
        var contentDisposition = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"audit-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return new AuditExportResult(payload, contentType, contentDisposition);
    }

    public async Task<AuditIntegrityStatusResponse> GetIntegrityStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("/api/admin/audit/integrity/status", cancellationToken);
        await EnsureSuccessAsync(response, nameof(GetIntegrityStatusAsync));

        var payload = await response.Content.ReadFromJsonAsync<AuditIntegrityStatusResponse>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("AdminService returned an empty integrity status payload.");
    }

    public async Task<AuditIntegrityHistoryResponse> GetIntegrityHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            [nameof(page)] = page.ToString(CultureInfo.InvariantCulture),
            [nameof(pageSize)] = pageSize.ToString(CultureInfo.InvariantCulture)
        };

        var requestUri = QueryHelpers.AddQueryString("/api/admin/audit/integrity/history", parameters);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        await EnsureSuccessAsync(response, nameof(GetIntegrityHistoryAsync));

        var payload = await response.Content.ReadFromJsonAsync<AuditIntegrityHistoryResponse>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("AdminService returned an empty integrity history payload.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new AuditForwardingException($"AdminService audit endpoint call '{operation}' failed with status {(int)response.StatusCode}.",
            response.StatusCode, body);
    }
}
