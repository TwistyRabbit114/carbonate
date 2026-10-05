using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Users;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Platform.Users;

/// <summary>Users and roles (FR-38). The Director and the Operations Manager administer accounts.</summary>
[Route("api/users")]
public class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.UserManage)]
    public async Task<ActionResult<PagedResult<UserListItem>>> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        Ok(await users.ListAsync(query, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.UserManage)]
    [ProducesResponseType<UserListItem>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserListItem>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await users.CreateAsync(request, ct));

    /// <summary>
    /// Change roles, name or active state. There must always be at least one active Director, and a role
    /// change or deactivation ends the person's current sessions.
    /// </summary>
    [HttpPatch("{userId:guid}")]
    [HasPermission(PermissionCodes.UserManage)]
    public async Task<ActionResult<UserListItem>> Update(Guid userId, UpdateUserRequest request, CancellationToken ct) =>
        Ok(await users.UpdateAsync(userId, request, ct));
}

/// <summary>The audit trail (FR-37). Read only; entries are never changed.</summary>
[Route("api/audit")]
public class AuditController(IAuditQueryService audit) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.AuditView)]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> List([FromQuery] AuditQuery query, CancellationToken ct) =>
        Ok(await audit.ListAsync(query, ct));
}
