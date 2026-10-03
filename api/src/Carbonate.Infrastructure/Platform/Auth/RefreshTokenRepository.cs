using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Platform.Auth;

internal sealed class RefreshTokenRepository(CemDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct) => await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeChainAsync(RefreshToken token, DateTime now, CancellationToken ct)
    {
        // Each rotation points at its replacement, so following the links reaches the live token.
        var current = token;
        while (true)
        {
            current.RevokedAt ??= now;

            if (current.ReplacedByTokenId is null)
            {
                break;
            }

            var next = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenId == current.ReplacedByTokenId, ct);
            if (next is null)
            {
                break;
            }

            current = next;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
