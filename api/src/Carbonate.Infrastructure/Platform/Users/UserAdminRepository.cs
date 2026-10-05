using Carbonate.Application.Common;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Users;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Platform.Users;

internal sealed class UserAdminRepository(CemDbContext db) : IUserAdminRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        var accounts = db.Users.AsNoTracking();

        if (query.IsActive is { } active)
        {
            accounts = accounts.Where(u => u.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role.Trim();
            accounts = accounts.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim();
            accounts = accounts.Where(u => u.FullName.Contains(text) || u.Email.Contains(text) || u.EmployeeNumber.Contains(text));
        }

        var total = await accounts.CountAsync(ct);
        var rows = await accounts
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync(ct);

        return new PagedResult<UserListItem>
        {
            Items = [.. rows.Select(ToItem)],
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
        };
    }

    public async Task<UserListItem?> GetAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(Projection).FirstOrDefaultAsync(ct);
        return row is null ? null : ToItem(row);
    }

    public Task<AppUser?> FindForUpdateAsync(Guid userId, CancellationToken ct) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public Task<bool> EmployeeNumberExistsAsync(string employeeNumber, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.EmployeeNumber == employeeNumber, ct);

    public async Task<Dictionary<string, Guid>> RoleIdsAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var wanted = names.ToList();
        return await db.Roles.Where(r => wanted.Contains(r.Name)).ToDictionaryAsync(r => r.Name, r => r.RoleId, ct);
    }

    public void Add(AppUser user, IEnumerable<Guid> roleIds, DateTime now)
    {
        db.Users.Add(user);
        foreach (var roleId in roleIds)
        {
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId, GrantedAt = now });
        }
    }

    public void SetRoles(AppUser user, IReadOnlyCollection<Guid> roleIds, DateTime now)
    {
        var existing = user.UserRoles.ToList();
        db.UserRoles.RemoveRange(existing.Where(ur => !roleIds.Contains(ur.RoleId)));

        // Added explicitly: a composite key is set by the caller, so EF would otherwise take a new link for an existing one.
        foreach (var roleId in roleIds.Where(id => existing.All(ur => ur.RoleId != id)))
        {
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId, GrantedAt = now });
        }
    }

    public Task<int> CountActiveDirectorsAsync(CancellationToken ct) =>
        db.Users.CountAsync(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == RoleNames.Director), ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            // Two administrators created the same person at once.
            var field = sql.Message.Contains("EmployeeNumber", StringComparison.OrdinalIgnoreCase) ? "employeeNumber" : "email";
            throw ProblemException.Validation(new Dictionary<string, string[]> { [field] = ["Someone already has that value."] });
        }
    }

    private static readonly System.Linq.Expressions.Expression<Func<AppUser, UserRow>> Projection = u => new UserRow
    {
        User = u,
        Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
    };

    private static UserListItem ToItem(UserRow row) => new()
    {
        UserId = row.User.UserId,
        EmployeeNumber = row.User.EmployeeNumber,
        Email = row.User.Email,
        FullName = row.User.FullName,
        EmploymentType = row.User.EmploymentType,
        IsActive = row.User.IsActive,
        MfaEnabled = row.User.MfaEnabled,
        Roles = [.. row.Roles.Order()],
        LastLoginAt = row.User.LastLoginAt,
    };

    private sealed class UserRow
    {
        public AppUser User { get; set; } = null!;
        public List<string> Roles { get; set; } = [];
    }
}

internal sealed class AuditReadRepository(CemDbContext db) : IAuditReadRepository
{
    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        var entries = db.AuditEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            var entity = query.Entity.Trim();
            entries = entries.Where(a => a.EntityName == entity);
        }

        if (query.From is { } from)
        {
            entries = entries.Where(a => a.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            entries = entries.Where(a => a.OccurredAt <= to);
        }

        var total = await entries.CountAsync(ct);
        var items = await (
                from a in entries.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.AuditId)
                    .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                join u in db.Users on a.UserId equals u.UserId into users
                from u in users.DefaultIfEmpty()
                orderby a.OccurredAt descending, a.AuditId descending
                select new AuditEntryDto
                {
                    AuditId = a.AuditId,
                    UserId = a.UserId,
                    UserName = u == null ? null : u.FullName,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    OccurredAt = a.OccurredAt,
                    IpAddress = a.IpAddress,
                    BeforeJson = a.BeforeJson,
                    AfterJson = a.AfterJson,
                })
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto> { Items = items, Page = query.Page, PageSize = query.PageSize, Total = total };
    }
}
