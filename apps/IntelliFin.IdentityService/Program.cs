using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

// Dev token issuing endpoint
app.MapPost("/auth/dev-token", (TokenRequest req, IConfiguration config) =>
{
    var jwtSection = config.GetSection("Jwt");
    var issuer = jwtSection.GetValue<string>("Issuer")!;
    var audience = jwtSection.GetValue<string>("Audience")!;
    var signingKey = jwtSection.GetValue<string>("SigningKey")!;
    var minutes = jwtSection.GetValue<int>("AccessTokenMinutes");

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, req.Username ?? "dev-user"),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new(ClaimTypes.Name, req.Username ?? "dev-user")
    };
    if (req.Roles is { Length: > 0 })
    {
        claims.AddRange(req.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
    }

    var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddMinutes(minutes > 0 ? minutes : 60),
        signingCredentials: creds
    );
    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = (minutes > 0 ? minutes : 60) * 60 });
});

app.MapGet("/", () => Results.Ok(new { name = "IntelliFin.IdentityService", status = "OK" }));

app.Run();

record TokenRequest(string? Username, string[]? Roles);
