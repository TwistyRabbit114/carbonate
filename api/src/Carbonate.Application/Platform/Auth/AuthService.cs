using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Domain.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Carbonate.Application.Platform.Auth;

public sealed partial class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IPasswordService passwords,
    ITotpService totp,
    ISecretProtector protector,
    IAuditService audit,
    IOptions<AuthOptions> options,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials =
        "That email and password do not match, or the account is temporarily locked.";

    private readonly AuthOptions _options = options.Value;

    public async Task<SessionResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var access = await users.FindByEmailAsync(request.Email.Trim(), ct);

        if (access is null || !access.User.IsActive)
        {
            passwords.VerifyDummy(request.Password);
            await audit.RecordAsync("auth.login_failed", nameof(AppUser), "unknown", null, null, null, ct);
            throw ProblemException.Unauthenticated(InvalidCredentials);
        }

        var user = access.User;
        var now = clock.GetUtcNow().UtcDateTime;

        if (IsLockedOut(user, now))
        {
            passwords.VerifyDummy(request.Password);
            throw ProblemException.Unauthenticated(InvalidCredentials);
        }

        if (!passwords.Verify(user, request.Password))
        {
            await RegisterFailureAsync(user, now, ct);
            throw ProblemException.Unauthenticated(InvalidCredentials);
        }

        if (RequiresMfa(access))
        {
            return new SessionResult(
                new SessionResponse(true, !user.MfaEnabled, tokens.CreateMfaToken(user.UserId), null, null),
                null,
                null);
        }

        return await CompleteSignInAsync(access, ip, ct);
    }

    public async Task<SessionResult> VerifyMfaAsync(MfaVerifyRequest request, string? ip, CancellationToken ct)
    {
        var userId = await tokens.ValidateMfaTokenAsync(request.MfaToken)
            ?? throw ProblemException.Unauthenticated("Your sign-in timed out. Please start again.");

        var access = await LoadActiveAsync(userId, ct);
        var user = access.User;
        var now = clock.GetUtcNow().UtcDateTime;

        if (IsLockedOut(user, now))
        {
            throw ProblemException.Unauthenticated(InvalidCredentials);
        }

        if (!user.MfaEnabled || user.MfaSecretEncrypted is null)
        {
            throw ProblemException.BusinessRule("Two-step sign-in has not been set up for this account yet.");
        }

        if (!totp.Verify(protector.Unprotect(user.MfaSecretEncrypted), request.Code))
        {
            await RegisterFailureAsync(user, now, ct);
            throw ProblemException.Unauthenticated("That code is not right. Check the app and try again.");
        }

        return await CompleteSignInAsync(access, ip, ct);
    }

    public async Task<MfaEnrolResponse> EnrolMfaAsync(Guid userId, CancellationToken ct)
    {
        var access = await LoadActiveAsync(userId, ct);
        var user = access.User;

        if (user.MfaEnabled)
        {
            throw ProblemException.BusinessRule("Two-step sign-in is already set up for this account.");
        }

        // The secret is stored but not trusted until the user proves they can produce a code.
        var secret = totp.GenerateSecret();
        user.MfaSecretEncrypted = protector.Protect(secret);
        await users.SaveChangesAsync(ct);

        return new MfaEnrolResponse(totp.BuildUri(secret, user.Email));
    }

    public async Task<SessionResult> ConfirmMfaAsync(Guid userId, string code, string? ip, CancellationToken ct)
    {
        var access = await LoadActiveAsync(userId, ct);
        var user = access.User;
        var now = clock.GetUtcNow().UtcDateTime;

        if (user.MfaEnabled || user.MfaSecretEncrypted is null)
        {
            throw ProblemException.BusinessRule("Start two-step setup first.");
        }

        if (!totp.Verify(protector.Unprotect(user.MfaSecretEncrypted), code))
        {
            await RegisterFailureAsync(user, now, ct);
            throw ProblemException.Unauthenticated("That code is not right. Check the app and try again.");
        }

        user.MfaEnabled = true;
        await audit.RecordAsync("auth.mfa_enrolled", nameof(AppUser), user.UserId.ToString(), null, null, user.UserId, ct);

        return await CompleteSignInAsync(access, ip, ct);
    }

    public async Task<SessionResult> RefreshAsync(string? refreshToken, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw ProblemException.Unauthenticated("Please sign in.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var stored = await refreshTokens.FindByHashAsync(tokens.HashRefreshToken(refreshToken), ct)
            ?? throw ProblemException.Unauthenticated("Please sign in.");

        if (stored.RevokedAt is not null)
        {
            // A token that was already rotated is being replayed: assume it was stolen and end the session.
            LogTokenReuse(stored.UserId);
            await refreshTokens.RevokeChainAsync(stored, now, ct);
            await audit.RecordAsync("auth.token_reuse", nameof(AppUser), stored.UserId.ToString(), null, null, stored.UserId, ct);
            throw ProblemException.Unauthenticated("Please sign in again.");
        }

        if (stored.ExpiresAt <= now)
        {
            throw ProblemException.Unauthenticated("Your session has ended. Please sign in again.");
        }

        var access = await users.FindByIdAsync(stored.UserId, ct);
        if (access is null || !access.User.IsActive || IsLockedOut(access.User, now))
        {
            await refreshTokens.RevokeChainAsync(stored, now, ct);
            throw ProblemException.Unauthenticated("Please sign in again.");
        }

        // The rotated token keeps the original expiry, so the session is capped at SessionHours from login.
        var (token, hash) = tokens.CreateRefreshToken();
        var replacement = new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = hash,
            ExpiresAt = stored.ExpiresAt,
            CreatedByIp = ip ?? "",
        };
        stored.RevokedAt = now;
        stored.ReplacedByTokenId = replacement.TokenId;
        await refreshTokens.AddAsync(replacement, ct);
        await refreshTokens.SaveChangesAsync(ct);

        return new SessionResult(
            ToSession(tokens.CreateAccessToken(access.User, access.Roles, access.Permissions)),
            token,
            replacement.ExpiresAt);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var stored = await refreshTokens.FindByHashAsync(tokens.HashRefreshToken(refreshToken), ct);
        if (stored is null)
        {
            return;
        }

        await refreshTokens.RevokeChainAsync(stored, clock.GetUtcNow().UtcDateTime, ct);
        await audit.RecordAsync("auth.logout", nameof(AppUser), stored.UserId.ToString(), null, null, stored.UserId, ct);
    }

    public async Task<MeResponse> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var access = await LoadActiveAsync(userId, ct);
        var user = access.User;

        return new MeResponse(
            new UserSummary(user.UserId, user.FullName, user.Email, user.EmploymentType.ToString()),
            access.Roles,
            access.Permissions);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}")]
    private partial void LogTokenReuse(Guid userId);

    private async Task<UserAccess> LoadActiveAsync(Guid userId, CancellationToken ct)
    {
        var access = await users.FindByIdAsync(userId, ct);
        if (access is null || !access.User.IsActive)
        {
            throw ProblemException.Unauthenticated("Please sign in.");
        }

        return access;
    }

    private async Task<SessionResult> CompleteSignInAsync(UserAccess access, string? ip, CancellationToken ct)
    {
        var user = access.User;
        var now = clock.GetUtcNow().UtcDateTime;

        user.LastLoginAt = now;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        var (token, hash) = tokens.CreateRefreshToken();
        var refresh = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = hash,
            ExpiresAt = now.AddHours(_options.SessionHours),
            CreatedByIp = ip ?? "",
        };
        await refreshTokens.AddAsync(refresh, ct);
        await users.SaveChangesAsync(ct);
        await audit.RecordAsync("auth.login", nameof(AppUser), user.UserId.ToString(), null, null, user.UserId, ct);

        return new SessionResult(
            ToSession(tokens.CreateAccessToken(user, access.Roles, access.Permissions)),
            token,
            refresh.ExpiresAt);
    }

    private async Task RegisterFailureAsync(AppUser user, DateTime now, CancellationToken ct)
    {
        user.AccessFailedCount++;
        var lockedOut = user.AccessFailedCount >= _options.LockoutThreshold;
        if (lockedOut)
        {
            user.LockoutEnd = now.AddMinutes(_options.LockoutMinutes);
            user.AccessFailedCount = 0;
        }

        await users.SaveChangesAsync(ct);
        await audit.RecordAsync(lockedOut ? "auth.lockout" : "auth.login_failed", nameof(AppUser),
            user.UserId.ToString(), null, null, user.UserId, ct);
    }

    private static bool IsLockedOut(AppUser user, DateTime now) => user.LockoutEnd is not null && user.LockoutEnd > now;

    private static bool RequiresMfa(UserAccess access) => access.Roles.Any(RoleNames.MfaRequired.Contains);

    private SessionResponse ToSession(AccessToken token)
    {
        var seconds = (int)(token.ExpiresAt - clock.GetUtcNow().UtcDateTime).TotalSeconds;
        return new SessionResponse(false, false, null, token.Value, seconds);
    }
}
