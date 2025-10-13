using System.ComponentModel.DataAnnotations;

namespace IntelliFin.AdminService.Contracts.Requests;

public class ArgoCdRollbackRequest
{
    [Range(0, int.MaxValue)]
    public int RevisionId { get; set; }
}
