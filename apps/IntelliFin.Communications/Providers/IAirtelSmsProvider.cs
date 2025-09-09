using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;

namespace IntelliFin.Communications.Providers;

public interface IAirtelSmsProvider : ISmsProvider
{
    Task<bool> ValidateAirtelNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    
    Task<decimal> GetAirtelRateAsync(CancellationToken cancellationToken = default);
    
    Task<string> GetAirtelStatusAsync(CancellationToken cancellationToken = default);
}