using System.Collections.Generic;

namespace IntelliFin.IdentityService.Models;

public class AuthorizationResult
{
    public bool IsAuthorized { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvaluatedItem { get; set; }
    public string? RequiredPermission { get; set; }
    public RuleEvaluationResult[] RuleResults { get; set; } = Array.Empty<RuleEvaluationResult>();

    public static AuthorizationResult Authorized(string reason, string evaluatedItem, string? requiredPermission = null) => new()
    {
        IsAuthorized = true,
        Reason = reason,
        EvaluatedItem = evaluatedItem,
        RequiredPermission = requiredPermission
    };

    public static AuthorizationResult Denied(string reason, string evaluatedItem, string? requiredPermission = null) => new()
    {
        IsAuthorized = false,
        Reason = reason,
        EvaluatedItem = evaluatedItem,
        RequiredPermission = requiredPermission
    };
}
