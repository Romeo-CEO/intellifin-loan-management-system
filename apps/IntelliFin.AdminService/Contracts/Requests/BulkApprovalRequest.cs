using System;
using System.Collections.Generic;

namespace IntelliFin.AdminService.Contracts.Requests;

public class BulkApprovalRequest
{
    public Guid TaskId { get; set; }
    public List<string> UserIds { get; set; } = new();
}
