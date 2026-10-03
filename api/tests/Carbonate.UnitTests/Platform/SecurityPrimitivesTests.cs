using System.Security.Cryptography;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Platform.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using OtpNet;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

public class SecurityPrimitivesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "carbonate-tests",
        Audience = "carbonate-tests",
        SigningKey = "test-signing-key-that-is-at-least-32-chars",
    };

    private static TokenService NewTokens(TimeProvider? clock = null, JwtOptions? jwt = null) =>
        new(Options.Create(jwt ?? Jwt), Options.Create(new AuthOptions()), clock ?? new FixedTime(Now));

    [Fact]
    public void Access_token_carries_the_roles_and_permissions_and_expires_in_fifteen_minutes()
    {
        var user = new AppUser { FullName = "Sarah M.", Email = "sarah@example.test" };

        var token = NewTokens().CreateAccessToken(user, ["EventManager"], ["event.create", "event.edit"]);

        var parsed = new JsonWebToken(token.Value);
        parsed.Subject.ShouldBe(user.UserId.ToString());
        parsed.Claims.Where(c => c.Type == ClaimNames.Role).Select(c => c.Value).ShouldBe(["EventManager"]);
        parsed.Claims.Where(c => c.Type == ClaimNames.Permission).Select(c => c.Value)
            .ShouldBe(["event.create", "event.edit"], ignoreOrder: true);
        token.ExpiresAt.ShouldBe(Now.UtcDateTime.AddMinutes(15));
    }

    [Fact]
    public async Task An_mfa_token_validates_back_to_the_same_user()
    {
        var tokens = NewTokens();
        var userId = Guid.NewGuid();

        var result = await tokens.ValidateMfaTokenAsync(tokens.CreateMfaToken(userId));

        result.ShouldBe(userId);
    }

    [Fact]
    public async Task An_access_token_is_not_accepted_as_an_mfa_token()
    {
        var tokens = NewTokens();
        var access = tokens.CreateAccessToken(new AppUser(), ["Director"], ["audit.view"]);

        var result = await tokens.ValidateMfaTokenAsync(access.Value);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task An_mfa_token_expires_after_five_minutes()
    {
        var issuedAt = new FixedTime(Now);
        var token = NewTokens(issuedAt).CreateMfaToken(Guid.NewGuid());

        var later = NewTokens(new FixedTime(Now.AddMinutes(6)));

        (await later.ValidateMfaTokenAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        var forged = NewTokens(jwt: new JwtOptions
        {
            Issuer = Jwt.Issuer,
            Audience = Jwt.Audience,
            SigningKey = "a-completely-different-signing-key-123456",
        }).CreateMfaToken(Guid.NewGuid());

        (await NewTokens().ValidateMfaTokenAsync(forged)).ShouldBeNull();
    }

    [Fact]
    public async Task A_garbage_token_is_rejected_without_throwing()
    {
        (await NewTokens().ValidateMfaTokenAsync("not-a-jwt")).ShouldBeNull();
    }

    [Fact]
    public void A_short_signing_key_is_refused()
    {
        var tokens = NewTokens(jwt: new JwtOptions { Issuer = "x", Audience = "x", SigningKey = "too-short" });

        Should.Throw<InvalidOperationException>(() => tokens.CreateMfaToken(Guid.NewGuid()));
    }

    [Fact]
    public void Refresh_tokens_are_random_and_only_their_hash_is_stored()
    {
        var tokens = NewTokens();

        var first = tokens.CreateRefreshToken();
        var second = tokens.CreateRefreshToken();

        first.Token.ShouldNotBe(second.Token);
        first.Hash.ShouldBe(tokens.HashRefreshToken(first.Token));
        first.Hash.ShouldNotContain(first.Token);
        first.Hash.Length.ShouldBe(64);
    }

    [Fact]
    public void The_secret_protector_round_trips_and_does_not_store_plain_text()
    {
        var protector = NewProtector();

        var cipher = protector.Protect("JBSWY3DPEHPK3PXP");

        cipher.ShouldNotContain("JBSWY3DPEHPK3PXP");
        protector.Unprotect(cipher).ShouldBe("JBSWY3DPEHPK3PXP");
        protector.Protect("JBSWY3DPEHPK3PXP").ShouldNotBe(cipher);
    }

    [Fact]
    public void A_tampered_secret_fails_to_decrypt()
    {
        var protector = NewProtector();
        var bytes = Convert.FromBase64String(protector.Protect("secret"));
        bytes[^1] ^= 0xFF;

        Should.Throw<CryptographicException>(() => protector.Unprotect(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void The_secret_protector_refuses_a_key_of_the_wrong_size()
    {
        var options = Options.Create(new AuthOptions { MfaEncryptionKey = Convert.ToBase64String(new byte[16]) });

        Should.Throw<InvalidOperationException>(() => new SecretProtector(options));
    }

    [Fact]
    public void A_current_totp_code_verifies_and_a_wrong_one_does_not()
    {
        var totp = new TotpService();
        var secret = totp.GenerateSecret();
        var current = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        totp.Verify(secret, current).ShouldBeTrue();
        totp.Verify(secret, current == "000000" ? "111111" : "000000").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void Codes_that_are_not_six_digits_are_rejected(string code)
    {
        var totp = new TotpService();

        totp.Verify(totp.GenerateSecret(), code).ShouldBeFalse();
    }

    [Fact]
    public void The_enrolment_uri_names_the_app_and_the_account()
    {
        var uri = new TotpService().BuildUri("JBSWY3DPEHPK3PXP", "sarah@example.test");

        uri.ShouldStartWith("otpauth://totp/Carbonate:");
        uri.ShouldContain("secret=JBSWY3DPEHPK3PXP");
        uri.ShouldContain("issuer=Carbonate");
    }

    [Fact]
    public void Passwords_hash_and_verify_and_an_unknown_user_costs_the_same_work()
    {
        var passwords = new PasswordService();
        var user = new AppUser();
        user.PasswordHash = passwords.Hash(user, "correct horse battery staple");

        passwords.Verify(user, "correct horse battery staple").ShouldBeTrue();
        passwords.Verify(user, "wrong").ShouldBeFalse();
        user.PasswordHash.ShouldNotContain("correct horse");
        Should.NotThrow(() => passwords.VerifyDummy("anything"));
    }

    private static SecretProtector NewProtector() =>
        new(Options.Create(new AuthOptions { MfaEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }));

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
