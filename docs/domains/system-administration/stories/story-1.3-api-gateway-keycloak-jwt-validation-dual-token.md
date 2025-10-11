# Story 1.3: API Gateway Keycloak JWT Validation (Dual-Token Support)

### Metadata
- **ID**: 1.3 | **Points**: 5 | **Effort**: 3-5 days | **Priority**: P0
- **Dependencies**: 1.2 (User migration), API Gateway existing
- **Blocks**: 1.10, 1.11

### User Story
**As a** developer,  
**I want** API Gateway to validate both old ASP.NET Core Identity JWTs and new Keycloak JWTs,  
**so that** we support gradual service migration without breaking existing clients.

### Acceptance Criteria
1. API Gateway JWT middleware accepts two token issuers (IntelliFin.Identity and Keycloak)
2. Keycloak public key retrieved via JWKS endpoint for signature validation
3. Branch-scoped claims (branchId, branchName) extracted from Keycloak tokens
4. Existing authentication endpoints (`/api/auth/*`) remain functional
5. Token type logged in audit trail
6. 30-day dual-token support window configured

### Technical Highlights
```csharp
// Dual JWT validation in API Gateway
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Legacy", options =>
    {
        options.Authority = "https://identity.intellifin.local";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = "IntelliFin.IdentityService"
        };
    })
    .AddJwtBearer("Keycloak", options =>
    {
        options.Authority = "https://keycloak.intellifin.local/realms/IntelliFin";
        options.Audience = "api-gateway";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateAudience = true
        };
    });

services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Legacy", "Keycloak")
        .RequireAuthenticatedUser()
        .Build();
});
```

### Integration Verification
- **IV1**: All existing API endpoints validate successfully with old JWTs
- **IV2**: New Keycloak JWTs accepted and propagated to downstream services
- **IV3**: Performance <10ms additional latency for dual-token validation
