using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LBElectronica.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace LBElectronica.Server.Services;

public class JwtService(IConfiguration configuration)
{
    private readonly string _key = configuration["Jwt:Key"] ?? "dev-super-secret-key-change-me";
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "lb-electronica";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "lb-electronica-clients";

    public string Generate(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
