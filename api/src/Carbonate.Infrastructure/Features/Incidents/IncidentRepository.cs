using Carbonate.Application.Common;
using Carbonate.Application.Features.Incidents;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Stock;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Incidents;

/// <summary>FR-31 data access.</summary>
internal sealed class IncidentRepository(CemDbContext db) : IIncidentRepository
{
    public async Task<bool> EventIsReportableAsync(
        Guid eventId, Guid? mustBeCrewedUserId, CancellationToken ct) =>
        await db.Events.AnyAsync(
            e => e.EventId == eventId
                 && e.IsActive
                 && (mustBeCrewedUserId == null
                     || db.CrewAssignments.Any(c => c.EventId == e.EventId && c.UserId == mustBeCrewedUserId)),
            ct);

    public async Task<IncidentSubject> CheckSubjectAsync(
        Guid? assetId, Guid? stockItemId, CancellationToken ct)
    {
        if (assetId is { } asset)
        {
            var name = await db.EquipmentAssets
                .Where(a => a.AssetId == asset)
                .Select(a => db.StockItems
                                 .Where(i => i.StockItemId == a.StockItemId)
                                 .Select(i => i.Name)
                                 .FirstOrDefault() + " " + a.SerialNumber)
                .FirstOrDefaultAsync(ct);

            return new IncidentSubject(name is not null, false, name?.Trim() ?? "");
        }

        if (stockItemId is { } item)
        {
            var name = await db.StockItems
                .Where(i => i.StockItemId == item)
                .Select(i => i.Name)
                .FirstOrDefaultAsync(ct);

            return new IncidentSubject(false, name is not null, name ?? "");
        }

        return new IncidentSubject(false, false, "");
    }

    public void Add(IncidentReport incident) => db.IncidentReports.Add(incident);

    public async Task<IncidentReport?> FindAsync(Guid incidentId, CancellationToken ct) =>
        await db.IncidentReports.FirstOrDefaultAsync(i => i.IncidentId == incidentId, ct);

    public async Task<IncidentRow?> GetRowAsync(Guid incidentId, CancellationToken ct) =>
        await Project(db.IncidentReports.AsNoTracking().Where(i => i.IncidentId == incidentId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<IncidentRow>> ListForEventAsync(Guid eventId, CancellationToken ct) =>
        await Project(db.IncidentReports
                .AsNoTracking()
                .Where(i => i.EventId == eventId)
                .OrderByDescending(i => i.ReportedAt))
            .ToListAsync(ct);

    public async Task<PagedResult<IncidentRow>> ListAsync(
        IncidentListQuery query, Guid? visibleToUserId, CancellationToken ct)
    {
        var rows = db.IncidentReports.AsNoTracking();

        if (query.AssetId is { } assetId)
        {
            // The whole history of one piece of equipment, which is how a repeated failure gets spotted.
            rows = rows.Where(i => i.AssetId == assetId);
        }

        if (visibleToUserId is { } userId)
        {
            rows = rows.Where(i => db.CrewAssignments.Any(c => c.EventId == i.EventId && c.UserId == userId));
        }

        var total = await rows.CountAsync(ct);

        var page = await Project(rows
                .OrderByDescending(i => i.ReportedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize))
            .ToListAsync(ct);

        return new PagedResult<IncidentRow>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
        };
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // Inline inside this method on purpose. EF translates the lambda into SQL and cannot translate a
    // call to a method of ours — that compiles and then throws at runtime on every query.
    private IQueryable<IncidentRow> Project(IQueryable<IncidentReport> incidents) =>
        incidents.Select(i => new IncidentRow(
            i.IncidentId,
            i.EventId,
            i.AssetId,
            i.StockItemId,
            i.AssetId == null
                ? db.StockItems.Where(s => s.StockItemId == i.StockItemId).Select(s => s.Name).FirstOrDefault() ?? ""
                : db.EquipmentAssets
                      .Where(a => a.AssetId == i.AssetId)
                      .Select(a => db.StockItems
                                       .Where(s => s.StockItemId == a.StockItemId)
                                       .Select(s => s.Name)
                                       .FirstOrDefault() + " " + a.SerialNumber)
                      .FirstOrDefault() ?? "",
            i.ReportedByUserId,
            db.Users.Where(u => u.UserId == i.ReportedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "",
            i.IncidentType,
            i.Quantity,
            i.ReportedAt,
            i.Description,
            i.ResolutionNotes,
            i.ReplacementCost,
            i.PhotoBlobUri));
}
