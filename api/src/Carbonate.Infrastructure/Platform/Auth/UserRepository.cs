using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Platform.Auth;

internal sealed class UserRepository(CemDbContext db) : IUserRepository
{
    public async Task<UserAccess?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var user = await Query().FirstOrDefaultAsync(u => u.Email == email, ct);
        return user is null ? null : ToAccess(user);
    }

    public async Task<UserAccess?> FindByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await Query().FirstOrDefaultAsync(u => u.UserId == userId, ct);
        return user is null ? null : ToAccess(user);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private IQueryable<AppUser> Query() => db.Users
        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions)
        .ThenInclude(rp => rp.Permission)
        .AsSplitQuery();

    private static UserAccess ToAccess(AppUser user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().Order().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct().Order().ToList();
        return new UserAccess(user, roles, permissions);
    }
}
