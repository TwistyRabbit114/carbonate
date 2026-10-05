using Carbonate.Application.Common;
using Carbonate.Application.Features.Venues;
using Carbonate.Domain.Features.Venues;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Venues;

internal sealed class VenueRepository(CemDbContext db) : IVenueRepository
{
    //----------------------------------------------------------\\
    //                              VENUES
    //----------------------------------------------------------\\

    public async Task<PagedResult<Venue>> SearchAsync(
        string? text, bool includeInactive, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Venues.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(v => v.IsActive);
        }
        if (text is not null)
        {
            query = query.Where(v => v.Name.Contains(text) || v.Address.Contains(text));
        }

        var total = await query.CountAsync(ct);
        //venue id breaks ties so two venues with the same name never swap pages
        var items = await query
            .OrderBy(v => v.Name).ThenBy(v => v.VenueId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Venue> { Items = items, Page = page, PageSize = pageSize, Total = total };
    }

    public Task<Venue?> FindAsync(Guid venueId, CancellationToken ct) =>
        db.Venues.FirstOrDefaultAsync(v => v.VenueId == venueId, ct);

    public Task<Venue?> FindThroughAssignmentAsync(Guid venueId, Guid userId, CancellationToken ct) =>
        db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == venueId
            && db.Events.Any(e => e.VenueId == venueId
                && e.IsActive
                && e.CrewAssignments.Any(a => a.UserId == userId)), ct);

    public void Add(Venue venue) => db.Venues.Add(venue);

    //----------------------------------------------------------\\
    //                              SITE VISITS
    //----------------------------------------------------------\\

    //newest recce first, it's the one that reflects the venue as it is now
    public async Task<IReadOnlyList<SiteVisitWithAuthor>> ListSiteVisitsAsync(Guid eventId, CancellationToken ct) =>
        await (
            from visit in db.SiteVisits.AsNoTracking()
            join user in db.Users on visit.ConductedByUserId equals user.UserId
            where visit.EventId == eventId
            orderby visit.VisitDate descending
            select new SiteVisitWithAuthor(visit, user.FullName))
        .ToListAsync(ct);

    public Task<SiteVisitWithAuthor?> FindSiteVisitAsync(Guid siteVisitId, CancellationToken ct) =>
        (
            from visit in db.SiteVisits
            join user in db.Users on visit.ConductedByUserId equals user.UserId
            where visit.SiteVisitId == siteVisitId
            select new SiteVisitWithAuthor(visit, user.FullName))
        .FirstOrDefaultAsync(ct);

    public Task<string?> ActiveUserNameAsync(Guid userId, CancellationToken ct) =>
        db.Users
            .Where(u => u.UserId == userId && u.IsActive)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

    public void Add(SiteVisit visit) => db.SiteVisits.Add(visit);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
