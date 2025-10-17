namespace IntelliFin.Shared.DomainModels.Entities;

public class TokenRevocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenId { get; set; } = string.Empty;
    public DateTime RevokedAtUtc { get; set; } = DateTime.UtcNow;
    public string RevokedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
