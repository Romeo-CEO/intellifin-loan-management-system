namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int PageNumber, int PageSize, int TotalCount);
