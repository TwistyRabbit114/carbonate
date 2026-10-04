namespace Carbonate.Application.Features.Calendar;

/// <summary>
/// Mints and checks the <c>state</c> parameter on the Google OAuth round trip (FR-40).
/// </summary>
/// <remarks>
/// <para>
/// Google redirects the <b>browser</b> to our callback, and a browser following a redirect sends no
/// bearer token. So the callback cannot be behind a permission attribute, and the user it belongs to
/// has to travel with the request. <c>state</c> is the parameter OAuth provides for exactly that.
/// </para>
/// <para>
/// It is a signed, short-lived token rather than a raw user id, because anything in a query string is
/// attacker-controlled: a bare id would let anyone connect their own Google account to someone else's
/// Carbonate user. Signing also doubles as the CSRF defence <c>state</c> is there to provide.
/// </para>
/// </remarks>
public interface ICalendarStateTokens
{
    /// <summary>Called by <c>POST /api/calendar/connect</c>, which <b>is</b> authenticated.</summary>
    string Create(Guid userId);

    /// <summary>
    /// The user this state belongs to, or null if it is missing, malformed, expired, signed by
    /// something else, or not a calendar state token.
    /// </summary>
    Task<Guid?> ValidateAsync(string? state);
}
