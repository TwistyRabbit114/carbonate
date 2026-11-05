using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Domain.Platform;
using Microsoft.Extensions.Options;

namespace Carbonate.Application.Platform.Auth;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public interface IPasswordChangeService
{
    /// <summary>The refresh token is the caller's own cookie, so this device can stay signed in.</summary>
    Task ChangeAsync(ChangePasswordRequest request, string? refreshToken, CancellationToken ct);
}

/// <summary>
/// A signed-in person changes their own password. They must know the current one, a wrong guess counts
/// towards lockout like a failed sign-in, and every other device is signed out.
/// </summary>
public sealed class PasswordChangeService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IPasswordService passwords,
    PasswordPolicy passwordPolicy,
    ITransactionRunner transactions,
    IAuditService audit,
    ICurrentUser currentUser,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IPasswordChangeService
{
    public async Task ChangeAsync(ChangePasswordRequest request, string? refreshToken, CancellationToken ct)
    {
        var access = await users.FindByIdAsync(currentUser.UserId, ct);
        if (access is null || !access.User.IsActive)
        {
            throw ProblemException.Unauthenticated("Please sign in.");
        }

        var user = access.User;
        var now = clock.GetUtcNow().UtcDateTime;

        // A 400 on the field, not a 401, so the screen does not read it as a dead session.
        const string WrongPassword = "That is not your current password, or the account is temporarily locked.";
        if (user.LockoutEnd > now)
        {
            passwords.VerifyDummy(request.CurrentPassword);
            throw Field("currentPassword", WrongPassword);
        }

        if (!passwords.Verify(user, request.CurrentPassword))
        {
            await RegisterFailureAsync(user, now, ct);
            throw Field("currentPassword", WrongPassword);
        }

        if (passwords.Verify(user, request.NewPassword))
        {
            throw Field("newPassword", "Choose a password different from your current one.");
        }

        var problems = await passwordPolicy.ValidateAsync(request.NewPassword, ct);
        if (problems.Count > 0)
        {
            throw Field("newPassword", [.. problems]);
        }

        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;

        var current = string.IsNullOrEmpty(refreshToken)
            ? null
            : await refreshTokens.FindByHashAsync(tokens.HashRefreshToken(refreshToken), ct);
        var keep = current is { RevokedAt: null } && current.UserId == user.UserId && current.ExpiresAt > now
            ? current.TokenId
            : (Guid?)null;

        await transactions.RunAsync(async t =>
        {
            await users.SaveChangesAsync(t);

            if (keep is Guid tokenId)
            {
                await refreshTokens.RevokeOthersForUserAsync(user.UserId, tokenId, now, t);
            }
            else
            {
                await refreshTokens.RevokeAllForUserAsync(user.UserId, now, t);
            }

            await audit.RecordAsync("user.password_changed", nameof(AppUser), user.UserId.ToString(), null,
                new { OtherSessionsEnded = true }, user.UserId, t);
            return 0;
        }, ct);
    }

    // Same counting as a failed sign-in, so this endpoint is no easier to guess against than login.
    private async Task RegisterFailureAsync(AppUser user, DateTime now, CancellationToken ct)
    {
        user.AccessFailedCount++;
        var lockedOut = user.AccessFailedCount >= options.Value.LockoutThreshold;
        if (lockedOut)
        {
            user.LockoutEnd = now.AddMinutes(options.Value.LockoutMinutes);
            user.AccessFailedCount = 0;
        }

        await users.SaveChangesAsync(ct);
        await audit.RecordAsync(lockedOut ? "auth.lockout" : "user.password_change_failed", nameof(AppUser),
            user.UserId.ToString(), null, null, user.UserId, ct);
    }

    private static ProblemException Field(string field, params string[] messages) =>
        ProblemException.Validation(new Dictionary<string, string[]> { [field] = messages });
}
