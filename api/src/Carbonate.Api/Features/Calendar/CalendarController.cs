using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Features.Calendar;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Calendar;

/// <summary>
/// Google Calendar connection (FR-40 to FR-42). Stubs until built; D owns the bodies. Sync is outbound
/// only, so there is no endpoint that reads from Google.
/// </summary>
[ApiController]
[Route("api/calendar")]
public class CalendarController : ApiControllerBase
{
    [HttpGet("connection")]
    [HasPermission(PermissionCodes.CalendarView)]
    public ActionResult<CalendarConnectionDto> Connection() => NotYetBuilt();

    /// <summary>Returns the Google consent URL for the narrow calendar.events scope.</summary>
    [HttpPost("connect")]
    [HasPermission(PermissionCodes.CalendarConnect)]
    public ActionResult<ConnectCalendarResponse> Connect() => NotYetBuilt();

    /// <summary>
    /// Where Google sends the browser back to. TODO(plan): the browser carries no bearer token on this
    /// redirect, so D must decide how the state parameter identifies the user; plan section 7.2 allows
    /// anonymous access on five endpoints and this is not one of them.
    /// </summary>
    [HttpGet("oauth/callback")]
    [HasPermission(PermissionCodes.CalendarConnect)]
    public IActionResult Callback([FromQuery] string? code, [FromQuery] string? state) => NotYetBuilt();

    [HttpDelete("connection")]
    [HasPermission(PermissionCodes.CalendarConnect)]
    public IActionResult Disconnect() => NotYetBuilt();
}
