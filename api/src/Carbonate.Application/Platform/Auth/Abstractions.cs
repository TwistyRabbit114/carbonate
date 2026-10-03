using Carbonate.Domain.Platform;

namespace Carbonate.Application.Platform.Auth;

/// <summary>A user together with the roles and permission codes they hold.</summary>
public record UserAccess(AppUser User, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public interface IUserRepository
{
    Task<UserAccess?> FindByEmailAsync(string email, CancellationToken ct);
    Task<UserAccess?> FindByIdAsync(Guid userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>Revokes this token and every token that was issued from it, directly or indirectly.</summary>
    Task RevokeChainAsync(RefreshToken token, DateTime now, CancellationToken ct);

    Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public record AccessToken(string Value, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions);

    /// <summary>A short-lived token that only proves the password step passed. It carries no permissions.</summary>
    string CreateMfaToken(Guid userId);

    /// <summary>Returns the user id from a valid MFA token, or null if it is invalid or expired.</summary>
    Task<Guid?> ValidateMfaTokenAsync(string token);

    /// <summary>A random opaque refresh token, and its SHA-256 hash for storage.</summary>
    (string Token, string Hash) CreateRefreshToken();

    string HashRefreshToken(string token);
}

public interface IPasswordService
{
    string Hash(AppUser user, string password);
    bool Verify(AppUser user, string password);

    /// <summary>Burns the same time as a real check, so an unknown email is not faster to reject.</summary>
    void VerifyDummy(string password);
}

public interface IPwnedPasswordChecker
{
    /// <summary>True if the password appears in known breaches. Fails open: false if the service is down.</summary>
    Task<bool> IsPwnedAsync(string password, CancellationToken ct);
}

public interface ITotpService
{
    string GenerateSecret();
    string BuildUri(string secret, string accountName);
    bool Verify(string secret, string code);
}

public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}

/// <summary>Who is calling and from where. Implemented over the HTTP context in the API.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
    bool HasPermission(string code);

    /// <summary>The widest scope any of the user's roles gives for this permission.</summary>
    PermissionScope ScopeOf(string code);
}
