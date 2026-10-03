using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Carbonate.Api.Platform.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    private const string RefreshCookie = "carbonate.refresh";

    /// <summary>Checks the password. Director and Accounts get an MFA token and must finish with a code.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Respond(await auth.LoginAsync(request, currentUser.IpAddress, ct));

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SessionResponse>> VerifyMfa(MfaVerifyRequest request, CancellationToken ct) =>
        Respond(await auth.VerifyMfaAsync(request, currentUser.IpAddress, ct));

    /// <summary>Starts two-step setup. Send the MFA token from login as the bearer token.</summary>
    [HttpPost("mfa/enrol")]
    [Authorize(Policy = AuthPolicies.MfaPending)]
    public async Task<ActionResult<MfaEnrolResponse>> EnrolMfa(CancellationToken ct) =>
        Ok(await auth.EnrolMfaAsync(currentUser.UserId, ct));

    /// <summary>Finishes two-step setup with a code from the authenticator app, and signs the user in.</summary>
    [HttpPost("mfa/confirm")]
    [Authorize(Policy = AuthPolicies.MfaPending)]
    public async Task<ActionResult<SessionResponse>> ConfirmMfa(MfaConfirmRequest request, CancellationToken ct) =>
        Respond(await auth.ConfirmMfaAsync(currentUser.UserId, request.Code, currentUser.IpAddress, ct));

    /// <summary>Swaps the refresh cookie for a new access token. The cookie is single-use and rotates.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken ct)
    {
        try
        {
            return Respond(await auth.RefreshAsync(Request.Cookies[RefreshCookie], currentUser.IpAddress, ct));
        }
        catch
        {
            // Whatever went wrong, do not leave a dead or stolen cookie in the browser.
            ClearCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], ct);
        ClearCookie();
        return NoContent();
    }

    private ActionResult<SessionResponse> Respond(SessionResult result)
    {
        if (result.RefreshToken is not null)
        {
            Response.Cookies.Append(RefreshCookie, result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                Expires = result.RefreshExpiresAt,
            });
        }

        return Ok(result.Body);
    }

    private void ClearCookie() =>
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/api/auth", Secure = true, SameSite = SameSiteMode.Strict });
}

[ApiController]
public class MeController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// The signed-in user, their roles and permissions. The SPA uses permissions only to decide
    /// navigation and buttons; hiding data is the server's job.
    /// </summary>
    [HttpGet("api/me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken ct) =>
        Ok(await auth.GetMeAsync(currentUser.UserId, ct));
}
