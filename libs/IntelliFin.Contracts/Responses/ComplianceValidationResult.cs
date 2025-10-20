using System.Collections.Generic;

namespace IntelliFin.Contracts.Responses;

public class ComplianceValidationResult
{
    public bool IsCompliant { get; set; }
    public double ComplianceScore { get; set; }
    public List<PermissionComplianceIssue> Violations { get; set; } = new();
}
