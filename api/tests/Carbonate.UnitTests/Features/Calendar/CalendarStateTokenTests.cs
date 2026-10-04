using System.Security.Claims;
using Carbonate.Application.Platform.Auth;
using Carbonate.Infrastructure.Features.Calendar;
using Carbonate.Infrastructure.Platform.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Carbonate.UnitTests.Features.Calendar;

/// <summary>
/// FR-40. The OAuth callback cannot be authenticated — a browser following Google's redirect carries
/// no token — so <c>state</c> is what proves who the round trip belongs to. These tests are about
/// what it must refuse.
/// </summary>
public class CalendarStateTokenTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "https://carbonate.test",
        Audience = "https://carbonate.test",
        SigningKey = "a-test-signing-key-that-is-long-enough-32",
    };

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 6, 0, 0, TimeSpan.Zero));

    private CalendarStateTokens Tokens() => new(Options.Create(Jwt), _clock);

    [Fact]
    public async Task A_state_it_minted_resolves_to_the_user()
    {
        var userId = Guid.NewGuid();
        var tokens = Tokens();

        (await tokens.ValidateAsync(tokens.Create(userId))).ShouldBe(userId);
    }

    [Fact]
    public async Task Two_states_for_the_same_user_are_different()
    {
        // The nonce. Otherwise two connect attempts in the same second are indistinguishable in a log.
        var tokens = Tokens();
        var userId = Guid.NewGuid();

        tokens.Create(userId).ShouldNotBe(tokens.Create(userId));
    }

    [Fact]
    public async Task Refuses_a_state_that_has_expired()
    {
        var tokens = Tokens();
        var state = tokens.Create(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromMinutes(6));

        (await tokens.ValidateAsync(state)).ShouldBeNull();
    }

    [Fact]
    public async Task Accepts_a_state_just_inside_its_window()
    {
        var tokens = Tokens();
        var state = tokens.Create(Guid.NewGuid());

        _clock.Advance(TimeSpan.FromMinutes(4));

        (await tokens.ValidateAsync(state)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Refuses_a_state_signed_with_a_different_key()
    {
        // The whole point: a user id in a query string is attacker-controlled. Without the signature,
        // anyone could connect their Google account to someone else's Carbonate user.
        var other = new CalendarStateTokens(
            Options.Create(new JwtOptions
            {
                Issuer = Jwt.Issuer,
                Audience = Jwt.Audience,
                SigningKey = "a-completely-different-key-also-long-32!!",
            }),
            _clock);

        (await Tokens().ValidateAsync(other.Create(Guid.NewGuid()))).ShouldBeNull();
    }

    [Fact]
    public async Task Refuses_an_access_token()
    {
        // Without the purpose check, a stolen access token could be spent connecting a calendar.
        var accessToken = TokenWithPurpose(null);

        (await Tokens().ValidateAsync(accessToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Refuses_an_mfa_token() =>
        (await Tokens().ValidateAsync(TokenWithPurpose(ClaimNames.MfaPurpose))).ShouldBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    public async Task Refuses_anything_malformed(string? state) =>
        (await Tokens().ValidateAsync(state)).ShouldBeNull();

    [Fact]
    public void Refuses_to_mint_a_state_for_nobody() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Tokens().Create(Guid.Empty));

    /// <summary>A correctly signed token that simply is not a calendar state.</summary>
    private string TokenWithPurpose(string? purpose)
    {
        var claims = new List<Claim> { new(ClaimNames.Subject, Guid.NewGuid().ToString()) };
        if (purpose is not null)
        {
            claims.Add(new Claim(ClaimNames.Purpose, purpose));
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Jwt.Issuer,
            Audience = Jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                TokenValidation.SigningKey(Jwt), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>A clock the test moves by hand, so an expiry test does not take five minutes.</summary>
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
