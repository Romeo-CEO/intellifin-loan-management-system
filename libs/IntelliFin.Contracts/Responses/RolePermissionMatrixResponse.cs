using System.Collections.Generic;

namespace IntelliFin.Contracts.Responses;

public class RolePermissionMatrixResponse
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}
