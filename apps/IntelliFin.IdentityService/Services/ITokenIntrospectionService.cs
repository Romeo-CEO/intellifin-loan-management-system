using IntelliFin.IdentityService.Models;

namespace IntelliFin.IdentityService.Services;

public interface ITokenIntrospectionService
{
    Task<IntrospectionResponse> IntrospectAsync(IntrospectionRequest request, CancellationToken cancellationToken = default);
}
