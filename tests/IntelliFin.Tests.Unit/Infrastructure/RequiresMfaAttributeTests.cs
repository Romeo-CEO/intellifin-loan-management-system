using System.Security.Claims;
using IntelliFin.AdminService.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace IntelliFin.Tests.Unit.Infrastructure;

public class RequiresMfaAttributeTests
{
    [Fact]
    public async Task MissingAmrClaim_ReturnsUnauthorized()
    {
        var attribute = new RequiresMfaAttribute();
        var context = CreateContext();

        await attribute.OnAuthorizationAsync(context);

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        var error = result.Value?.GetType().GetProperty("error")?.GetValue(result.Value)?.ToString();
        Assert.Equal("mfa_required", error);
    }

    [Fact]
    public async Task WithMfaClaim_AllowsRequest()
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new[]
        {
            new Claim("amr", "pwd,mfa"),
            new Claim("iat", issuedAt.ToString())
        };
        var attribute = new RequiresMfaAttribute();
        var context = CreateContext(claims);

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task ExpiredMfa_ReturnsUnauthorized()
    {
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds();
        var claims = new[]
        {
            new Claim("amr", "mfa"),
            new Claim("iat", issuedAt.ToString())
        };

        var attribute = new RequiresMfaAttribute { TimeoutMinutes = 5 };
        var context = CreateContext(claims);

        await attribute.OnAuthorizationAsync(context);

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        var error = result.Value?.GetType().GetProperty("error")?.GetValue(result.Value)?.ToString();
        Assert.Equal("mfa_expired", error);
    }

    private static AuthorizationFilterContext CreateContext(IEnumerable<Claim>? claims = null)
    {
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(claims ?? Array.Empty<Claim>(), "Test");
        httpContext.User = new ClaimsPrincipal(identity);

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor
        {
            DisplayName = "TestOperation"
        });

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }
}
