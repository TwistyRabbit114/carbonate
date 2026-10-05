using Carbonate.Application.Common;
using Carbonate.Application.Platform.Auth;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Common;

internal sealed class EventAccess(CemDbContext db, ICurrentUser currentUser) : IEventAccess
{
    public Task<bool> CanSeeEventAsync(Guid eventId, CancellationToken ct)
    {
        var viewAll = currentUser.HasPermission(PermissionCodes.EventViewAll);
        var userId = currentUser.UserId;

        return db.Events.AnyAsync(e => e.EventId == eventId
            && e.IsActive
            && (viewAll || e.CrewAssignments.Any(a => a.UserId == userId)), ct);
    }
}
