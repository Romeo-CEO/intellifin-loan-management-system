namespace IntelliFin.Shared.DomainModels.Entities;

public class TokenRevocation
{
    public Guid RevocationId { get; set; } = Guid.NewGuid();
    public string TokenId { get; set; } = string.Empty; // jti or opaque id
    public string UserId { get; set; } = string.Empty;
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;
    public string? RevokedBy { get; set; }
    public string? Reason { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
}