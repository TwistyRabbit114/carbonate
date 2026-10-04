using System.Security.Claims;
using Carbonate.Application.Platform.Auth;

namespace Carbonate.Api.Platform.Auth;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimNames.Subject), out var id) ? id : Guid.Empty;

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimNames.Role).Select(c => c.Value).ToList() ?? [];

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool HasPermission(string code) => Principal?.HasClaim(ClaimNames.Permission, code) == true;

    public PermissionScope ScopeOf(string code) =>
        Roles.Select(role => RolePermissionMatrix.ScopeFor(role, code)).DefaultIfEmpty(PermissionScope.None).Max();
}
