using System.Collections.Generic;

namespace IntelliFin.Contracts.Responses;

public class BulkBridgeOperationResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}
