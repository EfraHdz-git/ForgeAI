using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ForgeAI.Services.Auth;

public class JwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly int _expirationDays;

    public JwtService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"] ?? throw new ArgumentException("Jwt:Secret is required");
        _issuer = config["Jwt:Issuer"] ?? "ForgeAI";
        _expirationDays = config.GetValue<int>("Jwt:ExpirationDays", 30);
    }

    public string GenerateToken(int userId, string email, string tier, bool isAdmin = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("email", email),
            new Claim("tier", tier),
            new Claim("admin", isAdmin.ToString().ToLower())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_expirationDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = _issuer,
                ValidAudience = _issuer,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            }, out _);
        }
        catch
        {
            return null;
        }
    }

    public int? GetUserIdFromPrincipal(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return sub != null && int.TryParse(sub, out var id) ? id : null;
    }
}
