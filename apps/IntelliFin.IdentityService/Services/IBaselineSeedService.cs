using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using IntelliFin.IdentityService.Models.Domain;

namespace IntelliFin.IdentityService.Services;

public interface IBaselineSeedService
{
    Task<SeedResult> SeedBaselineDataAsync(CancellationToken cancellationToken = default);
    Task<SeedValidationResult> ValidateSeedDataAsync(CancellationToken cancellationToken = default);
}

public class SeedResult
{
    public int RolesCreated { get; set; }
    public int PermissionsCreated { get; set; }
    public int SoDRulesCreated { get; set; }
    public int RolesSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}
