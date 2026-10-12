using System.Security.Cryptography;
using System.Text;
using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.Auth;
using JobHunt.Api.Models.Responses.Auth;
using Microsoft.AspNetCore.Identity;

namespace JobHunt.Api.Services;

public record IssuedSession(AuthSessionResponse Response, string RefreshToken, DateTime RefreshExpiresAtUtc);

public class AuthSessionService(UserManager<ApplicationUser> users, RefreshTokenDataAccess tokens,
    JwtTokenService jwt)
{
    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<IssuedSession?> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }
        await users.ResetAccessFailedCountAsync(user);
        var rawToken = GenerateToken();
        var expiry = DateTime.UtcNow.AddDays(7);
        await tokens.SaveAsync(new RefreshToken
        {
            UserId = user.Id, TokenHash = Hash(rawToken), FamilyId = Guid.NewGuid(),
            SecurityStamp = user.SecurityStamp!, CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = expiry
        });
        return new(jwt.Create(user), rawToken, expiry);
    }

    public async Task<IssuedSession?> RefreshAsync(string? rawToken)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length > 256) return null;
        var replacement = GenerateToken();
        var token = await tokens.RotateAsync(Hash(rawToken), Hash(replacement));
        return token is null ? null : new(jwt.Create(token.User), replacement, token.ExpiresAtUtc);
    }

    public Task LogoutAsync(string? rawToken) => string.IsNullOrEmpty(rawToken) || rawToken.Length > 256
        ? Task.CompletedTask : tokens.RevokeAsync(Hash(rawToken));
}
