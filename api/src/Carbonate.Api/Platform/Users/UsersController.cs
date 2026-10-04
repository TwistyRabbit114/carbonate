using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Users;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Platform.Users;

/// <summary>Users and roles (FR-38). Stubs until the module lands; C owns the bodies.</summary>
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.UserManage)]
    public ActionResult<PagedResult<UserListItem>> List([FromQuery] UserListQuery query) => NotYetBuilt();

    [HttpPost]
    [HasPermission(PermissionCodes.UserManage)]
    [ProducesResponseType<UserListItem>(StatusCodes.Status201Created)]
    public ActionResult<UserListItem> Create(CreateUserRequest request) => NotYetBuilt();

    /// <summary>Change roles or deactivate. There must always be at least one active Director.</summary>
    [HttpPatch("{userId:guid}")]
    [HasPermission(PermissionCodes.UserManage)]
    public ActionResult<UserListItem> Update(Guid userId, UpdateUserRequest request) => NotYetBuilt();
}

/// <summary>The audit trail (FR-37, should have). Read only; entries are never changed.</summary>
[Route("api/audit")]
public class AuditController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.AuditView)]
    public ActionResult<PagedResult<AuditEntryDto>> List([FromQuery] AuditQuery query) => NotYetBuilt();
}
