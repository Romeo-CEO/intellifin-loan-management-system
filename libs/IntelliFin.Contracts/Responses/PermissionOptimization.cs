namespace IntelliFin.Contracts.Responses;

public class PermissionOptimization
{
    public string PermissionId { get; set; } = string.Empty;
    public int SuggestedReductionCount { get; set; }
}
