using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Seeding;

/// <summary>
/// First-run data every environment needs (NFR-02): roles, permissions, the role-permission links,
/// divisions and the admin board. It only adds what is missing, so running it twice changes nothing.
/// </summary>
public sealed class PlatformSeeder(CemDbContext db)
{
    private static readonly (string Code, string Name)[] Divisions =
    [
        ("CE", "Carbon Events"),
        ("CLM", "Carbon Logistics Management"),
    ];

    private static readonly (string Name, bool IsDone)[] AdminColumns =
    [
        ("Assigned", false),
        ("In Progress / Needs Review", false),
        ("Complete", true),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedRolesAsync(ct);
        await SeedDivisionsAsync(ct);
        await SeedAdminBoardAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        foreach (var (code, description) in RolePermissionMatrix.Permissions.Where(p => !existing.Contains(p.Code)))
        {
            db.Permissions.Add(new Permission { Code = code, Description = description });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.Include(r => r.RolePermissions).ToListAsync(ct);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);

        foreach (var name in RoleNames.All)
        {
            var role = roles.FirstOrDefault(r => r.Name == name);
            if (role is null)
            {
                role = new AppRole { Name = name, Description = name };
                db.Roles.Add(role);
            }

            var linked = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var code in RolePermissionMatrix.PermissionsFor(name))
            {
                var permission = permissions[code];
                if (!linked.Contains(permission.PermissionId))
                {
                    role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
                }
            }
        }
    }

    private async Task SeedDivisionsAsync(CancellationToken ct)
    {
        var existing = await db.Divisions.Select(d => d.Code).ToListAsync(ct);
        foreach (var (code, name) in Divisions.Where(d => !existing.Contains(d.Code)))
        {
            db.Divisions.Add(new Division { Code = code, Name = name });
        }
    }

    private async Task SeedAdminBoardAsync(CancellationToken ct)
    {
        if (await db.Boards.AnyAsync(b => b.BoardType == BoardType.Admin, ct))
        {
            return;
        }

        var board = new Board { BoardType = BoardType.Admin, Name = "Admin tasks", CreatedAt = DateTime.UtcNow };
        for (var i = 0; i < AdminColumns.Length; i++)
        {
            board.Columns.Add(new BoardColumn { Name = AdminColumns[i].Name, Position = i, IsDoneColumn = AdminColumns[i].IsDone });
        }

        db.Boards.Add(board);
    }
}
