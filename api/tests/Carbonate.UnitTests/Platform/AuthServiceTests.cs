using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IPasswordService _passwords = Substitute.For<IPasswordService>();
    private readonly ITotpService _totp = Substitute.For<ITotpService>();
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly FixedTime _clock = new(Now);
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _tokens.CreateAccessToken(Arg.Any<AppUser>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new AccessToken("jwt", Now.UtcDateTime.AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns(("raw-refresh", "hash-refresh"));
        _tokens.HashRefreshToken(Arg.Any<string>()).Returns(call => "hash-of-" + call.Arg<string>());
        _passwords.Verify(Arg.Any<AppUser>(), "right-password").Returns(true);
        _protector.Unprotect(Arg.Any<string>()).Returns("secret");

        _service = new AuthService(
            _users, _refreshTokens, _tokens, _passwords, _totp, _protector, _audit,
            Options.Create(new AuthOptions()), _clock, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_with_the_right_password_returns_an_access_token_and_a_refresh_token()
    {
        var user = GivenUser(RoleNames.EventManager);

        var result = await _service.LoginAsync(new LoginRequest(user.User.Email, "right-password"), "1.2.3.4", default);

        result.Body.AccessToken.ShouldBe("jwt");
        result.Body.MfaRequired.ShouldBeFalse();
        result.RefreshToken.ShouldBe("raw-refresh");
        result.RefreshExpiresAt.ShouldBe(Now.UtcDateTime.AddHours(8));
        user.User.LastLoginAt.ShouldBe(Now.UtcDateTime);
    }

    [Fact]
    public async Task Login_with_an_unknown_email_fails_the_same_way_as_a_wrong_password()
    {
        _users.FindByEmailAsync("nobody@example.test", Arg.Any<CancellationToken>()).Returns((UserAccess?)null);
        var known = GivenUser(RoleNames.EventManager);

        var unknown = await Should.ThrowAsync<ProblemException>(
            () => _service.LoginAsync(new LoginRequest("nobody@example.test", "x"), null, default));
        var wrong = await Should.ThrowAsync<ProblemException>(
            () => _service.LoginAsync(new LoginRequest(known.User.Email, "wrong"), null, default));

        unknown.Status.ShouldBe(401);
        wrong.Status.ShouldBe(401);
        unknown.Message.ShouldBe(wrong.Message);
        _passwords.Received(1).VerifyDummy("x");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_fifteen_minutes()
    {
        var user = GivenUser(RoleNames.EventManager);

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<ProblemException>(
                () => _service.LoginAsync(new LoginRequest(user.User.Email, "wrong"), null, default));
        }

        user.User.LockoutEnd.ShouldBe(Now.UtcDateTime.AddMinutes(15));
        user.User.AccessFailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_locked_account_is_refused_even_with_the_right_password()
    {
        var user = GivenUser(RoleNames.EventManager);
        user.User.LockoutEnd = Now.UtcDateTime.AddMinutes(5);

        var error = await Should.ThrowAsync<ProblemException>(
            () => _service.LoginAsync(new LoginRequest(user.User.Email, "right-password"), null, default));

        error.Status.ShouldBe(401);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_sign_in()
    {
        var user = GivenUser(RoleNames.CasualCrew);
        user.User.IsActive = false;

        var error = await Should.ThrowAsync<ProblemException>(
            () => _service.LoginAsync(new LoginRequest(user.User.Email, "right-password"), null, default));

        error.Status.ShouldBe(401);
    }

    [Theory]
    [InlineData(RoleNames.Director)]
    [InlineData(RoleNames.Accounts)]
    public async Task Director_and_Accounts_must_complete_the_second_step(string role)
    {
        var user = GivenUser(role);
        _tokens.CreateMfaToken(user.User.UserId).Returns("mfa-token");

        var result = await _service.LoginAsync(new LoginRequest(user.User.Email, "right-password"), null, default);

        result.Body.MfaRequired.ShouldBeTrue();
        result.Body.MfaEnrolmentRequired.ShouldBeTrue();
        result.Body.MfaToken.ShouldBe("mfa-token");
        result.Body.AccessToken.ShouldBeNull();
        result.RefreshToken.ShouldBeNull();
    }

    [Fact]
    public async Task A_user_with_two_step_set_up_is_not_asked_to_enrol_again()
    {
        var user = GivenUser(RoleNames.Director);
        user.User.MfaEnabled = true;

        var result = await _service.LoginAsync(new LoginRequest(user.User.Email, "right-password"), null, default);

        result.Body.MfaRequired.ShouldBeTrue();
        result.Body.MfaEnrolmentRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task The_right_code_completes_the_sign_in()
    {
        var user = GivenUser(RoleNames.Director);
        user.User.MfaEnabled = true;
        user.User.MfaSecretEncrypted = "protected";
        _tokens.ValidateMfaTokenAsync("mfa-token").Returns(user.User.UserId);
        _totp.Verify("secret", "123456").Returns(true);

        var result = await _service.VerifyMfaAsync(new MfaVerifyRequest("mfa-token", "123456"), null, default);

        result.Body.AccessToken.ShouldBe("jwt");
        result.RefreshToken.ShouldBe("raw-refresh");
    }

    [Fact]
    public async Task A_wrong_code_is_refused_and_counts_towards_lockout()
    {
        var user = GivenUser(RoleNames.Director);
        user.User.MfaEnabled = true;
        user.User.MfaSecretEncrypted = "protected";
        _tokens.ValidateMfaTokenAsync("mfa-token").Returns(user.User.UserId);
        _totp.Verify("secret", "000000").Returns(false);

        var error = await Should.ThrowAsync<ProblemException>(
            () => _service.VerifyMfaAsync(new MfaVerifyRequest("mfa-token", "000000"), null, default));

        error.Status.ShouldBe(401);
        user.User.AccessFailedCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_expired_or_forged_mfa_token_is_refused()
    {
        _tokens.ValidateMfaTokenAsync("bad").Returns((Guid?)null);

        var error = await Should.ThrowAsync<ProblemException>(
            () => _service.VerifyMfaAsync(new MfaVerifyRequest("bad", "123456"), null, default));

        error.Status.ShouldBe(401);
    }

    [Fact]
    public async Task Confirming_enrolment_with_the_right_code_turns_two_step_on()
    {
        var user = GivenUser(RoleNames.Accounts);
        user.User.MfaSecretEncrypted = "protected";
        _totp.Verify("secret", "123456").Returns(true);

        var result = await _service.ConfirmMfaAsync(user.User.UserId, "123456", null, default);

        user.User.MfaEnabled.ShouldBeTrue();
        result.Body.AccessToken.ShouldBe("jwt");
    }

    [Fact]
    public async Task Refreshing_rotates_the_token_and_keeps_the_original_expiry()
    {
        var user = GivenUser(RoleNames.EventManager);
        var sessionEnd = Now.UtcDateTime.AddHours(3);
        var stored = new RefreshToken { UserId = user.User.UserId, TokenHash = "hash-of-old", ExpiresAt = sessionEnd };
        _refreshTokens.FindByHashAsync("hash-of-old", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _service.RefreshAsync("old", null, default);

        result.RefreshToken.ShouldBe("raw-refresh");
        result.RefreshExpiresAt.ShouldBe(sessionEnd);
        stored.RevokedAt.ShouldBe(Now.UtcDateTime);
        stored.ReplacedByTokenId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_ends_the_whole_session()
    {
        var user = GivenUser(RoleNames.EventManager);
        var stored = new RefreshToken
        {
            UserId = user.User.UserId,
            TokenHash = "hash-of-old",
            ExpiresAt = Now.UtcDateTime.AddHours(3),
            RevokedAt = Now.UtcDateTime.AddMinutes(-1),
        };
        _refreshTokens.FindByHashAsync("hash-of-old", Arg.Any<CancellationToken>()).Returns(stored);

        var error = await Should.ThrowAsync<ProblemException>(() => _service.RefreshAsync("old", null, default));

        error.Status.ShouldBe(401);
        await _refreshTokens.Received(1).RevokeChainAsync(stored, Now.UtcDateTime, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_refresh_token_past_the_eight_hour_session_is_refused()
    {
        var user = GivenUser(RoleNames.EventManager);
        var stored = new RefreshToken
        {
            UserId = user.User.UserId,
            TokenHash = "hash-of-old",
            ExpiresAt = Now.UtcDateTime.AddSeconds(-1),
        };
        _refreshTokens.FindByHashAsync("hash-of-old", Arg.Any<CancellationToken>()).Returns(stored);

        var error = await Should.ThrowAsync<ProblemException>(() => _service.RefreshAsync("old", null, default));

        error.Status.ShouldBe(401);
    }

    [Fact]
    public async Task Refreshing_for_a_deactivated_user_fails_and_revokes_the_chain()
    {
        var user = GivenUser(RoleNames.CasualCrew);
        user.User.IsActive = false;
        var stored = new RefreshToken
        {
            UserId = user.User.UserId,
            TokenHash = "hash-of-old",
            ExpiresAt = Now.UtcDateTime.AddHours(3),
        };
        _refreshTokens.FindByHashAsync("hash-of-old", Arg.Any<CancellationToken>()).Returns(stored);

        await Should.ThrowAsync<ProblemException>(() => _service.RefreshAsync("old", null, default));

        await _refreshTokens.Received(1).RevokeChainAsync(stored, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refreshing_without_a_cookie_is_refused()
    {
        var error = await Should.ThrowAsync<ProblemException>(() => _service.RefreshAsync(null, null, default));

        error.Status.ShouldBe(401);
    }

    private UserAccess GivenUser(params string[] roles)
    {
        var user = new AppUser
        {
            Email = $"{Guid.NewGuid():N}@example.test",
            FullName = "Test User",
            IsActive = true,
        };
        var access = new UserAccess(user, roles, []);
        _users.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(access);
        _users.FindByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(access);
        return access;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
