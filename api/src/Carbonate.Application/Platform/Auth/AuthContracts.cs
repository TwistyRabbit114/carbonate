namespace Carbonate.Application.Platform.Auth;

public record LoginRequest(string Email, string Password);

public record MfaVerifyRequest(string MfaToken, string Code);

public record MfaConfirmRequest(string Code);

public record MfaEnrolResponse(string OtpauthUri);

/// <summary>
/// What the sign-in endpoints return. Either the user still owes a TOTP step (<see cref="MfaRequired"/>)
/// and gets a short-lived <see cref="MfaToken"/>, or they are in and get an <see cref="AccessToken"/>.
/// </summary>
public record SessionResponse(
    bool MfaRequired,
    bool MfaEnrolmentRequired,
    string? MfaToken,
    string? AccessToken,
    int? ExpiresInSeconds);

/// <summary>The response body plus the refresh token the controller puts in an HttpOnly cookie.</summary>
public record SessionResult(SessionResponse Body, string? RefreshToken, DateTime? RefreshExpiresAt);

public record UserSummary(Guid UserId, string FullName, string Email, string EmploymentType);

public record MeResponse(UserSummary User, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public interface IAuthService
{
    Task<SessionResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct);
    Task<SessionResult> VerifyMfaAsync(MfaVerifyRequest request, string? ip, CancellationToken ct);
    Task<MfaEnrolResponse> EnrolMfaAsync(Guid userId, CancellationToken ct);
    Task<SessionResult> ConfirmMfaAsync(Guid userId, string code, string? ip, CancellationToken ct);
    Task<SessionResult> RefreshAsync(string? refreshToken, string? ip, CancellationToken ct);
    Task LogoutAsync(string? refreshToken, CancellationToken ct);
    Task<MeResponse> GetMeAsync(Guid userId, CancellationToken ct);
}
