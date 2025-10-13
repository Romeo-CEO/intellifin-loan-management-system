using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IntelliFin.AdminService.Services;

public interface IManagerDirectoryService
{
    Task<IReadOnlyList<ManagerUserAssignment>> GetManagerUserAssignmentsAsync(CancellationToken cancellationToken);
}

public sealed record ManagerUserAssignment(
    string UserId,
    string UserName,
    string UserEmail,
    string? Department,
    string? JobTitle,
    string ManagerUserId,
    string ManagerName,
    string ManagerEmail,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? RiskLevel,
    IReadOnlyList<string> RiskIndicators,
    System.DateTime? LastLoginUtc,
    System.DateTime? AccessGrantedUtc);
