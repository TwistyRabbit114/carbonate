using System.Security.Claims;
using Carbonate.Application.Features.Calendar;
using Carbonate.Application.Platform.Auth;
using Carbonate.Infrastructure.Platform.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Carbonate.Infrastructure.Features.Calendar;

/// <summary>
/// The OAuth <c>state</c> parameter as a signed, five-minute token. See
/// <see cref="ICalendarStateTokens"/> for why the callback cannot be authenticated normally.
/// </summary>
/// <remarks>
/// Signed with the existing JWT key rather than stored in a new table: a state row would be a schema
/// change on the day before submission, for a value that lives five minutes. The trade-off is that a
/// state cannot be marked used, so it is replayable inside its window — bounded by Google's
/// authorisation codes being single-use, so a replay has no code to redeem.
/// </remarks>
internal sealed class CalendarStateTokens(IOptions<JwtOptions> jwtOptions, TimeProvider clock)
    : ICalendarStateTokens
{
    /// <summary>
    /// Long enough to sign in to Google and pick an account, short enough that a state left in a
    /// browser history is worthless.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>Marks the token as this and nothing else, so it can never be used as a sign-in.</summary>
    internal const string Purpose = "calendar_oauth";

    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public string Create(Guid userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);

        var now = clock.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimNames.Subject, userId.ToString()),
                new Claim(ClaimNames.Purpose, Purpose),
                // A nonce makes two states minted in the same second different, so one cannot be
                // mistaken for the other in a log.
                new Claim("nonce", Guid.NewGuid().ToString("N")),
            ]),
            IssuedAt = now,
            Expires = now.Add(Lifetime),
            SigningCredentials = new SigningCredentials(
                TokenValidation.SigningKey(_jwt), SecurityAlgorithms.HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }

    public async Task<Guid?> ValidateAsync(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        // Expiry is judged against the injected clock, not the machine's. The handler would otherwise
        // use DateTime.UtcNow directly, which makes the five-minute window untestable.
        var parameters = TokenValidation.Parameters(_jwt);
        parameters.LifetimeValidator = (_, expires, _, _) => expires > clock.GetUtcNow().UtcDateTime;

        TokenValidationResult result;
        try
        {
            result = await _handler.ValidateTokenAsync(state, parameters);
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            // Anything unreadable is simply not a state we issued.
            return null;
        }

        if (!result.IsValid)
        {
            return null;
        }

        // The purpose check is the important one: without it, an access token would be accepted here
        // and a stolen one could be spent connecting a calendar.
        var purpose = result.ClaimsIdentity.FindFirst(ClaimNames.Purpose)?.Value;
        if (purpose != Purpose)
        {
            return null;
        }

        var subject = result.ClaimsIdentity.FindFirst(ClaimNames.Subject)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}
