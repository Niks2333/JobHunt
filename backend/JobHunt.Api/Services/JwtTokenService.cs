using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Responses.Auth;
using Microsoft.IdentityModel.Tokens;

namespace JobHunt.Api.Services;

public class JwtTokenService(IConfiguration configuration)
{
    public AuthSessionResponse Create(ApplicationUser user)
    {
        var now = DateTime.UtcNow;
        var expiry = now.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 10));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim("sub", user.Id), new Claim("jti", Guid.NewGuid().ToString("N"))],
            now, expiry, new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expiry,
            new(user.Id, user.Email!, user.EmailConfirmed));
    }
}
