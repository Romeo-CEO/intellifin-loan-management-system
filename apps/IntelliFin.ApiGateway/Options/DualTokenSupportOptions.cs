namespace IntelliFin.ApiGateway.Options;

public class DualTokenSupportOptions
{
    public bool Enabled { get; set; } = true;
    public int WindowDays { get; set; } = 30;
    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }

    public DateTimeOffset GetLegacySupportEnd(DateTimeOffset reference)
    {
        if (EndUtc.HasValue)
        {
            return EndUtc.Value;
        }

        if (StartUtc.HasValue)
        {
            return StartUtc.Value.AddDays(WindowDays);
        }

        return reference.AddDays(WindowDays);
    }
}
