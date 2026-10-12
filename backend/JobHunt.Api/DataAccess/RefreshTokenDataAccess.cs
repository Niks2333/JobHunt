using JobHunt.Api.Data;
using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.DataAccess;

public class RefreshTokenDataAccess(JobHuntDbContext database)
{
    public async Task SaveAsync(RefreshToken token)
    {
        database.RefreshTokens.Add(token);
        await database.SaveChangesAsync();
    }

    public async Task<RefreshToken?> RotateAsync(string oldHash, string newHash)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        // Serialize rotation of the same token, including across simultaneous requests.
        var token = await database.RefreshTokens.FromSqlInterpolated(
                $"SELECT * FROM dbo.RefreshTokens WITH (UPDLOCK, HOLDLOCK) WHERE TokenHash = {oldHash}")
            .Include(item => item.User).SingleOrDefaultAsync();
        if (token is null) return null;
        var now = DateTime.UtcNow;
        if (token.RevokedAtUtc is not null || token.ExpiresAtUtc <= now ||
            token.SecurityStamp != token.User.SecurityStamp || token.User.LockoutEnd > now)
        {
            // Reusing an already-rotated token invalidates that entire login session.
            await database.RefreshTokens.Where(item => item.FamilyId == token.FamilyId && item.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAtUtc, now));
            await transaction.CommitAsync();
            return null;
        }
        token.RevokedAtUtc = now;
        var replacement = new RefreshToken
        {
            UserId = token.UserId, User = token.User, FamilyId = token.FamilyId,
            SecurityStamp = token.SecurityStamp, TokenHash = newHash,
            CreatedAtUtc = now, ExpiresAtUtc = token.ExpiresAtUtc
        };
        database.RefreshTokens.Add(replacement);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return replacement;
    }

    public async Task RevokeAsync(string hash)
    {
        var familyId = await database.RefreshTokens.Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.FamilyId).SingleOrDefaultAsync();
        if (familyId is null) return;
        await database.RefreshTokens.Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAtUtc, DateTime.UtcNow));
    }
}
