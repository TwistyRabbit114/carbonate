using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Platform.Auth;

internal sealed class PasswordResetRepository(CemDbContext db) : IPasswordResetRepository
{
    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);

    public Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task CloseOpenForUserAsync(Guid userId, DateTime now, CancellationToken ct) =>
        db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, now), ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
