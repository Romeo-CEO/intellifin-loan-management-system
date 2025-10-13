using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class ArgoCdSyncRequest
{
    public bool? Prune { get; set; }

    public bool? DryRun { get; set; }

    [Range(1, 10)]
    public int? RetryLimit { get; set; }
}
