using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Domain.Platform;
using Microsoft.Extensions.Options;

namespace Carbonate.Application.Platform.Auth;

/// <summary>Shown once to the administrator who issued it. Only its hash is stored.</summary>
public record PasswordResetIssued(string Token, DateTime ExpiresAt);

public record ResetPasswordRequest(string Token, string NewPassword);

public interface IPasswordResetRepository
{
    void Add(PasswordResetToken token);

    /// <summary>The token with this hash, tracked so it can be marked used. Null if there is none.</summary>
    Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>Ends every reset for this user that is still open, so only the newest one works.</summary>
    Task CloseOpenForUserAsync(Guid userId, DateTime now, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordResetService
{
    Task<PasswordResetIssued> IssueAsync(Guid userId, CancellationToken ct);
    Task ResetAsync(ResetPasswordRequest request, CancellationToken ct);
}

/// <summary>
/// Password reset without email. The office is small, so an administrator issues a single-use reset and
/// hands it to the person, who sets their own new password with it. The administrator never knows the
/// new password, and the reset dies after use, after a set time, or when a newer one is issued.
/// </summary>
public sealed class PasswordResetService(
    IPasswordResetRepository resets,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IPasswordService passwords,
    PasswordPolicy passwordPolicy,
    ITransactionRunner transactions,
    IAuditService audit,
    ICurrentUser currentUser,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IPasswordResetService
{
    private const string InvalidReset = "This reset link is not valid or has expired. Ask an administrator for a new one.";

    public async Task<PasswordResetIssued> IssueAsync(Guid userId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(PermissionCodes.UserManage))
        {
            throw ProblemException.Forbidden();
        }

        var target = await users.FindByIdAsync(userId, ct) ?? throw ProblemException.NotFound("That user was not found.");

        // Same rule as editing an account: otherwise anyone who manages users could take over a Director.
        if (target.Roles.Contains(RoleNames.Director) && !currentUser.Roles.Contains(RoleNames.Director))
        {
            throw ProblemException.Forbidden("Only the Director can reset a Director's password.");
        }

        if (!target.User.IsActive)
        {
            throw ProblemException.BusinessRule("That account is deactivated. Reactivate it first.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var (token, hash) = tokens.CreateRefreshToken();
        var reset = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(options.Value.PasswordResetMinutes),
            IssuedByUserId = currentUser.UserId,
        };

        await transactions.RunAsync(async t =>
        {
            await resets.CloseOpenForUserAsync(userId, now, t);
            resets.Add(reset);
            await resets.SaveChangesAsync(t);

            // The token itself never goes in the audit trail.
            await audit.RecordAsync("user.password_reset_issued", nameof(AppUser), userId.ToString(), null,
                new { reset.ExpiresAt }, currentUser.UserId, t);
            return 0;
        }, ct);

        return new PasswordResetIssued(token, reset.ExpiresAt);
    }

    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var reset = await resets.FindByHashAsync(tokens.HashRefreshToken(request.Token), ct);

        // One answer for every bad case, so the endpoint says nothing about which tokens or accounts exist.
        if (reset is null || reset.UsedAt is not null || reset.ExpiresAt <= now)
        {
            throw Invalid();
        }

        var access = await users.FindByIdAsync(reset.UserId, ct);
        if (access is null || !access.User.IsActive)
        {
            throw Invalid();
        }

        var problems = await passwordPolicy.ValidateAsync(request.NewPassword, ct);
        if (problems.Count > 0)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]> { ["newPassword"] = [.. problems] });
        }

        var user = access.User;
        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        reset.UsedAt = now;

        await transactions.RunAsync(async t =>
        {
            await users.SaveChangesAsync(t);
            await resets.SaveChangesAsync(t);

            // Anyone still signed in as this person is signed out.
            await refreshTokens.RevokeAllForUserAsync(user.UserId, now, t);
            await audit.RecordAsync("user.password_reset", nameof(AppUser), user.UserId.ToString(), null,
                new { SessionsEnded = true }, user.UserId, t);
            return 0;
        }, ct);
    }

    private static ProblemException Invalid() =>
        ProblemException.Validation(new Dictionary<string, string[]> { ["token"] = [InvalidReset] });
}
