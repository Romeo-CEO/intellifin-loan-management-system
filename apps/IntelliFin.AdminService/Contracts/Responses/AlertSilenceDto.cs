using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record AlertSilenceDto
{
    public string SilenceId { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public IReadOnlyCollection<AlertSilenceMatcherDto> Matchers { get; init; } = Array.Empty<AlertSilenceMatcherDto>();
}

public sealed record AlertSilenceMatcherDto
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsRegex { get; init; }
}
