namespace IntelliFin.AdminService.Contracts.Responses;

public class MfaEnrollmentResponse
{
    public string QrCodeDataUri { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
}
