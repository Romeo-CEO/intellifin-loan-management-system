using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Services;

public interface IOfflineAuditMergeService
{
    Task<OfflineMergeResult> MergeAsync(OfflineMergeRequest request, string userId, CancellationToken cancellationToken);
}
