using Carbonate.Api.Platform.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.IntegrationTests.Support;

/// <summary>Endpoints that exist only in tests, to exercise the authorisation rules in isolation.</summary>
[ApiController]
[Route("api/test")]
public class PermissionProbeController : ControllerBase
{
    [HttpGet("needs-audit-view")]
    [HasPermission("audit.view")]
    public IActionResult NeedsAuditView() => Ok();

    /// <summary>Declares nothing, so it falls to the deny-by-default policy.</summary>
    [HttpGet("no-declaration")]
    public IActionResult NoDeclaration() => Ok();

    [HttpGet("open")]
    [AllowAnonymous]
    public IActionResult Open() => Ok();

    [HttpPost("echo")]
    [AllowAnonymous]
    public IActionResult Echo(EchoBody body) => Ok(body);

    public record EchoBody(string Name);
}
