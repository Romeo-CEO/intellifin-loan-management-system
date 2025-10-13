using System;
using System.Collections.Generic;
using IntelliFin.AdminService.Contracts.Requests;

namespace IntelliFin.AdminService.Services;

public interface IAlertmanagerClient
{
    Task<IReadOnlyCollection<AlertmanagerSilence>> GetSilencesAsync(CancellationToken cancellationToken);
    Task<AlertmanagerSilence> CreateSilenceAsync(CreateAlertSilenceRequest request, CancellationToken cancellationToken);
}

public sealed record AlertmanagerSilence
{
    public string Id { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public IReadOnlyCollection<AlertmanagerMatcher> Matchers { get; init; } = Array.Empty<AlertmanagerMatcher>();
}

public sealed record AlertmanagerMatcher(string Name, string Value, bool IsRegex);
