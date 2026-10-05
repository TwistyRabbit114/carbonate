using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;

namespace Carbonate.Application.Platform.Users;

public interface IUserService
{
    Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct);
    Task<UserListItem> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserListItem> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken ct);
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct);
}

/// <summary>
/// Users and their roles (FR-38, NFR-29). The Director and the Operations Manager manage accounts, so
/// the client administers their own system without us.
/// </summary>
public sealed class UserService(
    IUserAdminRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordService passwords,
    PasswordPolicy passwordPolicy,
    ITransactionRunner transactions,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider clock) : IUserService
{
    private const string UserNotFound = "That user was not found.";

    public async Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        RequireManage();

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);
        return await users.ListAsync(query, ct);
    }

    public async Task<UserListItem> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        RequireManage();

        var roleNames = request.Roles.Distinct().ToList();
        var errors = new Dictionary<string, string[]>();
        var roleIds = await users.RoleIdsAsync(roleNames, ct);

        var unknown = roleNames.Where(r => !roleIds.ContainsKey(r)).ToList();
        if (unknown.Count > 0)
        {
            errors["roles"] = [$"Not a role: {string.Join(", ", unknown)}."];
        }

        RequireDirectorToGrantDirector(roleNames);

        var email = request.Email.Trim();
        if (await users.EmailExistsAsync(email, ct))
        {
            errors["email"] = ["Someone already has that email address."];
        }

        if (await users.EmployeeNumberExistsAsync(request.EmployeeNumber, ct))
        {
            errors["employeeNumber"] = ["Someone already has that employee number."];
        }

        var passwordProblems = await passwordPolicy.ValidateAsync(request.InitialPassword, ct);
        if (passwordProblems.Count > 0)
        {
            errors["initialPassword"] = [.. passwordProblems];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var account = new AppUser
        {
            EmployeeNumber = request.EmployeeNumber,
            Email = email,
            FullName = request.FullName.Trim(),
            EmploymentType = request.EmploymentType,
            IsActive = true,
        };
        account.PasswordHash = passwords.Hash(account, request.InitialPassword);

        await transactions.RunAsync(async token =>
        {
            users.Add(account, roleNames.Select(r => roleIds[r]), now);
            await users.SaveChangesAsync(token);

            // The password is never written to the audit trail.
            await audit.RecordAsync("user.create", nameof(AppUser), account.UserId.ToString(), null,
                new { account.Email, account.EmployeeNumber, account.FullName, account.EmploymentType, Roles = roleNames },
                currentUser.UserId, token);
            return account.UserId;
        }, ct);

        return await users.GetAsync(account.UserId, ct) ?? throw ProblemException.NotFound(UserNotFound);
    }

    public async Task<UserListItem> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken ct)
    {
        RequireManage();

        var target = await users.FindForUpdateAsync(userId, ct) ?? throw ProblemException.NotFound(UserNotFound);
        var currentRoles = target.UserRoles.Select(ur => ur.Role.Name).Order().ToList();
        var actorIsDirector = currentUser.Roles.Contains(RoleNames.Director);

        // Only a Director may touch a Director's account or hand out the Director role. Otherwise anyone
        // who can manage users could make themselves a Director.
        if (!actorIsDirector && currentRoles.Contains(RoleNames.Director))
        {
            throw ProblemException.Forbidden("Only the Director can change a Director's account.");
        }

        var newRoles = request.Roles?.Distinct().Order().ToList();
        if (newRoles is not null)
        {
            RequireDirectorToGrantDirector(newRoles);
        }

        var isSelf = target.UserId == currentUser.UserId;
        var rolesChange = newRoles is not null && !newRoles.SequenceEqual(currentRoles);
        if (isSelf && rolesChange)
        {
            throw ProblemException.BusinessRule("You cannot change your own roles. Ask another administrator.");
        }

        if (isSelf && request.IsActive == false)
        {
            throw ProblemException.BusinessRule("You cannot deactivate your own account.");
        }

        Dictionary<string, Guid>? roleIds = null;
        if (rolesChange)
        {
            roleIds = await users.RoleIdsAsync(newRoles!, ct);
            var unknown = newRoles!.Where(r => !roleIds.ContainsKey(r)).ToList();
            if (unknown.Count > 0)
            {
                throw ProblemException.Validation(new Dictionary<string, string[]> { ["roles"] = [$"Not a role: {string.Join(", ", unknown)}."] });
            }
        }

        var before = new { target.FullName, target.IsActive, Roles = currentRoles };
        var now = clock.GetUtcNow().UtcDateTime;
        var deactivating = request.IsActive == false && target.IsActive;

        if (request.FullName is { } fullName)
        {
            target.FullName = fullName.Trim();
        }

        if (request.IsActive is { } active)
        {
            target.IsActive = active;
            if (active)
            {
                // Bringing someone back should not leave them locked out from before.
                target.LockoutEnd = null;
                target.AccessFailedCount = 0;
            }
        }

        // A role change or a deactivation must end the person's current sessions, not wait for them to lapse.
        var endSessions = rolesChange || deactivating;
        if (endSessions)
        {
            target.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        if (rolesChange)
        {
            users.SetRoles(target, [.. newRoles!.Select(r => roleIds![r])], now);
        }

        await transactions.RunAsync(async token =>
        {
            await users.SaveChangesAsync(token);

            if (endSessions)
            {
                await refreshTokens.RevokeAllForUserAsync(target.UserId, now, token);
            }

            // Checked after the change is saved, inside the transaction, so a change that would leave no
            // active Director is undone.
            if (await users.CountActiveDirectorsAsync(token) < 1)
            {
                throw ProblemException.BusinessRule("There must always be at least one active Director.");
            }

            await audit.RecordAsync("user.update", nameof(AppUser), target.UserId.ToString(), before,
                new { target.FullName, target.IsActive, Roles = (IReadOnlyList<string>)(newRoles ?? currentRoles), SessionsEnded = endSessions },
                currentUser.UserId, token);
            return 0;
        }, ct);

        return await users.GetAsync(userId, ct) ?? throw ProblemException.NotFound(UserNotFound);
    }

    private void RequireManage()
    {
        if (!currentUser.HasPermission(PermissionCodes.UserManage))
        {
            throw ProblemException.Forbidden();
        }
    }

    private void RequireDirectorToGrantDirector(IEnumerable<string> roles)
    {
        if (roles.Contains(RoleNames.Director) && !currentUser.Roles.Contains(RoleNames.Director))
        {
            throw ProblemException.Forbidden("Only the Director can give someone the Director role.");
        }
    }
}

public sealed class AuditQueryService(IAuditReadRepository repository, ICurrentUser currentUser) : IAuditQueryService
{
    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        if (!currentUser.HasPermission(PermissionCodes.AuditView))
        {
            throw ProblemException.Forbidden();
        }

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);
        query.From = query.From is null ? null : DateTimes.ToUtc(query.From.Value);
        query.To = query.To is null ? null : DateTimes.ToUtc(query.To.Value);
        return await repository.ListAsync(query, ct);
    }
}
