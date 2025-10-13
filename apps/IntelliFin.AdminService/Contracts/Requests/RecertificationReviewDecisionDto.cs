using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Requests;

public class RecertificationReviewDecisionDto
{
    public Guid ReviewId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<string>? RolesToRevoke { get; set; }
}
