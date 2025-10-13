# Story 1.11: Branch-Scoped JWT Claims Implementation

### Metadata
- **ID**: 1.11 | **Points**: 8 | **Effort**: 5-7 days | **Priority**: P1
- **Dependencies**: 1.3 (API Gateway Keycloak integration), user data migration
- **Blocks**: None

### User Story
**As a** developer,  
**I want** JWT tokens to include branch-scoped claims (branchId, branchName, branchRegion),  
**so that** API services can filter data by branch without additional database queries.

### Acceptance Criteria
1. Keycloak user attributes `branchId`, `branchName`, `branchRegion` mapped to user profiles
2. Keycloak client protocol mapper configured to include branch claims in JWT access tokens
3. API Gateway middleware extracts branch claims and adds to request context (`HttpContext.Items`)
4. Service authorization policies updated to use branch claims for data filtering
5. Performance testing validates 80% reduction in authorization queries (NFR3 target)
6. Branch claim audit: Log when user accesses data outside their branch (potential SoD violation)
7. Documentation updated with branch claim usage patterns for developers

### Protocol Mapper Configuration
```json
// Keycloak Protocol Mapper for branch claims
{
  "protocol": "openid-connect",
  "protocolMapper": "oidc-usermodel-attribute-mapper",
  "name": "branch-claims-mapper",
  "config": {
    "user.attribute": "branchId",
    "claim.name": "branch_id",
    "jsonType.label": "int",
    "id.token.claim": "true",
    "access.token.claim": "true",
    "userinfo.token.claim": "true"
  }
}
```

### API Gateway Middleware
```csharp
public class BranchClaimMiddleware
{
    private static readonly string[] BranchIdClaimTypes = ["branch_id", "branchId"];
    private static readonly string[] BranchNameClaimTypes = ["branch_name", "branchName"];
    private static readonly string[] BranchRegionClaimTypes = ["branch_region", "branchRegion"];
    private const string BranchSwitchPermission = "branch:switch_context";

    private readonly RequestDelegate _next;
    private readonly ILogger<BranchClaimMiddleware> _logger;

    public BranchClaimMiddleware(RequestDelegate next, ILogger<BranchClaimMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var branchIdValue = GetFirstClaimValue(context.User, BranchIdClaimTypes);
            var branchNameValue = GetFirstClaimValue(context.User, BranchNameClaimTypes);
            var branchRegionValue = GetFirstClaimValue(context.User, BranchRegionClaimTypes);

            if (!string.IsNullOrWhiteSpace(branchIdValue) && int.TryParse(branchIdValue, out var branchId))
            {
                context.Items[BranchClaimItemKeys.BranchId] = branchId;
                context.Items[BranchClaimItemKeys.BranchIdRaw] = branchIdValue;
            }

            if (!string.IsNullOrWhiteSpace(branchNameValue))
            {
                context.Items[BranchClaimItemKeys.BranchName] = branchNameValue;
            }

            if (!string.IsNullOrWhiteSpace(branchRegionValue))
            {
                context.Items[BranchClaimItemKeys.BranchRegion] = branchRegionValue;
            }

            await LogPotentialBranchOverrideAsync(context, branchIdValue, branchNameValue, branchRegionValue);
        }

        await _next(context);
    }
}

// Usage in downstream services (via forwarded headers)
services
    .AddAuthorization(options =>
    {
        options.AddPolicy("BranchScope", policy =>
            policy.RequireAssertion(ctx =>
            {
                var httpContext = (ctx.Resource as HttpContext)!;
                var branchIdHeader = httpContext.Request.Headers["X-Branch-Id"].FirstOrDefault();
                var branchIdClaim = ctx.User.FindFirst("branch_id")?.Value;
                return string.IsNullOrEmpty(branchIdClaim) || branchIdHeader == branchIdClaim;
            }));
    });
```

> **Context keys**: `BranchClaimItemKeys` exposes strongly-typed keys (`BranchId`, `BranchIdRaw`, `BranchName`, `BranchRegion`) that the gateway populates before proxying requests. All proxied calls also receive `X-Branch-Id`, `X-Branch-Name`, and `X-Branch-Region` headers for service-side filtering.

> **Audit trail**: The middleware records a `BRANCH_SCOPE_OVERRIDE_ATTEMPT` audit event whenever a user without the `branch:switch_context` permission queries for a branch ID different from their token claim.

### Integration Verification
- **IV1**: Existing branch-based queries refactored to use JWT claims (backward compatible)
- **IV2**: Multi-branch users (managers) validated to have correct branch claim hierarchy
- **IV3**: Performance improvement measured: Loan application list query latency reduced from 200ms to <120ms
