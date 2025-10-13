namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record UserRolesDto(
    string UserId,
    IReadOnlyCollection<UserRoleDto> Roles,
    IReadOnlyCollection<SodExceptionSummaryDto> ActiveSodExceptions);
