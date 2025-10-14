using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Services;

namespace IntelliFin.Tests.Integration.AdminService;

public sealed class StubCamundaTokenProvider : ICamundaTokenProvider
{
    private readonly string _token;

    public StubCamundaTokenProvider(string token)
    {
        _token = token;
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_token);
    }
}
