# Story 1.10: Rotating Refresh Token Implementation

### Metadata
- **ID**: 1.10 | **Points**: 5 | **Effort**: 3-5 days | **Priority**: P1
- **Dependencies**: 1.3 (API Gateway Keycloak integration)
- **Blocks**: None

### User Story
**As a** security engineer,  
**I want** Keycloak refresh tokens to rotate on every refresh operation,  
**so that** we reduce security risk from long-lived refresh token theft.

### Acceptance Criteria
1. Keycloak realm configured with `Rotate Refresh Tokens` policy enabled
2. Redis tracking of refresh token families for revocation chain detection
3. Token revocation endpoint (`/api/auth/revoke`) extended to revoke entire token family
4. Frontend updated to handle refresh token rotation (store new refresh token from response)
5. Token theft detection: If revoked token in family used, entire family invalidated and user logged out
6. Audit events logged for refresh operations and token family revocations
7. Documentation updated with refresh token rotation flow diagrams

### Implementation
```csharp
// Keycloak realm configuration
{
  "realm": "IntelliFin",
  "refreshTokenMaxReuse": 0,  // No reuse allowed - forces rotation
  "revokeRefreshToken": true,  // Old tokens immediately revoked
  "refreshTokenLifespan": 1800  // 30 minutes
}

// Redis token family tracking
public class TokenFamilyService
{
    private readonly IDistributedCache _redis;
    
    public async Task TrackTokenFamilyAsync(string tokenId, string familyId)
    {
        var family = await _redis.GetStringAsync($"token_family:{familyId}");
        var tokenList = family != null ? JsonSerializer.Deserialize<List<string>>(family) : new List<string>();
        tokenList.Add(tokenId);
        await _redis.SetStringAsync($"token_family:{familyId}", JsonSerializer.Serialize(tokenList), 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) });
    }
    
    public async Task<bool> IsTokenFamilyCompromisedAsync(string tokenId, string familyId)
    {
        var family = await _redis.GetStringAsync($"token_family:{familyId}");
        if (family == null) return false;
        
        var tokenList = JsonSerializer.Deserialize<List<string>>(family);
        
        // If trying to use a token that's not the latest in the family, it's compromised
        return tokenList.LastOrDefault() != tokenId && tokenList.Contains(tokenId);
    }
    
    public async Task RevokeEntireTokenFamilyAsync(string familyId)
    {
        await _redis.SetStringAsync($"token_family_revoked:{familyId}", "true", 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) });
    }
}
```

### Integration Verification
- **IV1**: Existing refresh token logic updated without breaking active sessions
- **IV2**: Performance test confirms refresh operation <500ms
- **IV3**: Token theft simulation validates revocation chain works
