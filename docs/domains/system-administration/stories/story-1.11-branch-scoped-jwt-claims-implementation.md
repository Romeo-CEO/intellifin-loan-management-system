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
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var branchId = context.User.FindFirst("branch_id")?.Value;
            var branchName = context.User.FindFirst("branch_name")?.Value;
            var branchRegion = context.User.FindFirst("branch_region")?.Value;
            
            context.Items["BranchId"] = branchId != null ? int.Parse(branchId) : (int?)null;
            context.Items["BranchName"] = branchName;
            context.Items["BranchRegion"] = branchRegion;
        }
        
        await _next(context);
    }
}

// Usage in services
public async Task<List<LoanApplication>> GetLoanApplicationsAsync(HttpContext context)
{
    var branchId = context.Items["BranchId"] as int?;
    
    if (branchId.HasValue)
    {
        // Filter by branch from JWT claim - no DB query needed!
        return await _db.LoanApplications
            .Where(la => la.BranchId == branchId.Value)
            .ToListAsync();
    }
    else
    {
        // Multi-branch user (manager) - return all
        return await _db.LoanApplications.ToListAsync();
    }
}
```

### Integration Verification
- **IV1**: Existing branch-based queries refactored to use JWT claims (backward compatible)
- **IV2**: Multi-branch users (managers) validated to have correct branch claim hierarchy
- **IV3**: Performance improvement measured: Loan application list query latency reduced from 200ms to <120ms
