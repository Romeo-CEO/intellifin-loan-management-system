using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;

namespace IntelliFin.Communications.Providers;

public interface IMtnSmsProvider : ISmsProvider
{
    Task<bool> ValidateMtnNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    
    Task<decimal> GetMtnRateAsync(CancellationToken cancellationToken = default);
    
    Task<string> GetMtnStatusAsync(CancellationToken cancellationToken = default);
}