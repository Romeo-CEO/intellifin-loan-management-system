using System.Threading;
using System.Threading.Tasks;

namespace IntelliFin.AdminService.Services;

public interface ICamundaTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
