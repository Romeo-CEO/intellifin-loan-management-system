using System.Security.Claims;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Models.Domain;
using IntelliFin.IdentityService.Services;
using AuditEvent = IntelliFin.IdentityService.Models.AuditEvent;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.IdentityService.Tests.Services;

public class BaselineSeedServiceTests
{
    private static LmsDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new LmsDbContext(options);
    }

    private static RoleManager<ApplicationRole> CreateRoleManagerMock(bool roleExistsInitially, List<Claim>? addedClaimsOut)
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        var validators = new List<IRoleValidator<ApplicationRole>> { new RoleValidator<ApplicationRole>() };
        var roleManager = new Mock<RoleManager<ApplicationRole>>(store.Object, validators, new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null);

        // Track created roles
        roleManager.Setup(r => r.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Success);

        // RoleExistsAsync behaviour configured by caller per run
        roleManager.Setup(r => r.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(roleExistsInitially);

        roleManager.Setup(r => r.AddClaimAsync(It.IsAny<ApplicationRole>(), It.IsAny<Claim>()))
            .Callback<ApplicationRole, Claim>((_, c) => addedClaimsOut?.Add(c))
            .ReturnsAsync(IdentityResult.Success);

        return roleManager.Object;
    }

    private static IAuditService CreateAuditMock(List<AuditEvent> captured)
    {
        var mock = new Mock<IAuditService>();
        mock.Setup(a => a.LogAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEvent, CancellationToken>((e, _) => captured.Add(e))
            .Returns(Task.CompletedTask);
        return mock.Object;
    }

    private static void WriteSeedJson(string baseDir)
    {
        var folder = Path.Combine(baseDir, "Data", "Seeds");
        Directory.CreateDirectory(folder);
        var json = """
        {
          "roles": [
            {
              "roleName": "Test Role",
              "description": "Test role",
              "permissions": ["perm:one", "perm:two"]
            }
          ],
          "sodRules": [
            {
              "ruleName": "sod-test",
              "description": "Test rule",
              "conflictingPermissions": ["a:b", "c:d"],
              "enforcement": "strict"
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(folder, "BaselineRolesSeed.json"), json);
    }

    [Fact]
    public async Task Seed_CreatesRoles_SoDRules_And_IsIdempotent()
    {
        // Arrange
        var baseDir = AppContext.BaseDirectory;
        WriteSeedJson(baseDir);

        var addedClaims = new List<Claim>();
        var audits = new List<AuditEvent>();

        using var db1 = CreateDbContext(Guid.NewGuid().ToString());
        var roleManagerFirst = CreateRoleManagerMock(roleExistsInitially: false, addedClaimsOut: addedClaims);
        var auditService = CreateAuditMock(audits);
        var logger = Mock.Of<ILogger<BaselineSeedService>>();
        var service = new BaselineSeedService(db1, roleManagerFirst, auditService, logger);

        // Act: first seed run
        var result1 = await service.SeedBaselineDataAsync();

        // Assert first run
        Assert.True(result1.Success);
        Assert.Equal(1, result1.RolesCreated);
        Assert.Equal(2, result1.PermissionsCreated);
        Assert.Equal(1, result1.SoDRulesCreated);
        Assert.Single(db1.SoDRules);
        Assert.True(addedClaims.Count >= 2);
        Assert.True(audits.Any(e => e.Action == "RoleCreated"));
        Assert.True(audits.Any(e => e.Action == "SoDRuleCreated"));

        // Arrange: second run should skip role creation
        var addedClaimsSecond = new List<Claim>();
        using var db2 = CreateDbContext(Guid.NewGuid().ToString());
        // Copy SoDRule from db1 to db2 to simulate persistent state
        foreach (var rule in db1.SoDRules.AsNoTracking().ToList())
        {
            db2.SoDRules.Add(new SoDRule
            {
                Id = rule.Id,
                Name = rule.Name,
                Description = rule.Description,
                Enforcement = rule.Enforcement,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system",
                ConflictingPermissions = rule.ConflictingPermissions
            });
        }
        await db2.SaveChangesAsync();

        var roleManagerSecond = CreateRoleManagerMock(roleExistsInitially: true, addedClaimsOut: addedClaimsSecond);
        var service2 = new BaselineSeedService(db2, roleManagerSecond, auditService, logger);

        // Act: second seed run
        var result2 = await service2.SeedBaselineDataAsync();

        // Assert idempotency
        Assert.True(result2.Success);
        Assert.Equal(0, result2.RolesCreated);
        Assert.Equal(0, result2.PermissionsCreated);
        Assert.Equal(0, result2.SoDRulesCreated); // already exists
        Assert.Single(db2.SoDRules);
        Assert.Empty(addedClaimsSecond); // no new claims added
    }

    [Fact]
    public async Task Validate_ReturnsCreatableFlags_WhenNotExisting()
    {
        // Arrange
        var baseDir = AppContext.BaseDirectory;
        WriteSeedJson(baseDir);

        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var roleManager = CreateRoleManagerMock(roleExistsInitially: false, addedClaimsOut: null);
        var auditService = CreateAuditMock(new List<AuditEvent>());
        var logger = Mock.Of<ILogger<BaselineSeedService>>();
        var service = new BaselineSeedService(db, roleManager, auditService, logger);

        // Act
        var validation = await service.ValidateSeedDataAsync();

        // Assert
        Assert.Contains(validation.RoleChecks, r => r.RoleName == "Test Role" && r.CanCreate);
        Assert.Contains(validation.SoDChecks, r => r.RuleName == "sod-test" && r.CanCreate);
    }
}
