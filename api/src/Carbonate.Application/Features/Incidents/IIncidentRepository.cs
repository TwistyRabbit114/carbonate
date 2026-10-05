using Carbonate.Application.Common;
using Carbonate.Domain.Features.Stock;

namespace Carbonate.Application.Features.Incidents;

public interface IIncidentRepository
{
    /// <summary>True when the event exists, is active, and this user may report against it.</summary>
    Task<bool> EventIsReportableAsync(Guid eventId, Guid? mustBeCrewedUserId, CancellationToken ct);

    Task<IncidentSubject> CheckSubjectAsync(Guid? assetId, Guid? stockItemId, CancellationToken ct);

    void Add(IncidentReport incident);

    Task<IncidentReport?> FindAsync(Guid incidentId, CancellationToken ct);

    /// <summary>Null when the incident does not exist. The blob URI is needed to mint a download link.</summary>
    Task<IncidentRow?> GetRowAsync(Guid incidentId, CancellationToken ct);

    Task<IReadOnlyList<IncidentRow>> ListForEventAsync(Guid eventId, CancellationToken ct);

    Task<PagedResult<IncidentRow>> ListAsync(IncidentListQuery query, Guid? visibleToUserId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <param name="Name">What the incident is against, for display. Empty when neither id resolved.</param>
public sealed record IncidentSubject(bool AssetExists, bool StockItemExists, string Name);

/// <summary>An incident joined to the names it displays, straight out of the query.</summary>
public sealed record IncidentRow(
    Guid IncidentId,
    Guid EventId,
    Guid? AssetId,
    Guid? StockItemId,
    string SubjectName,
    Guid ReportedByUserId,
    string ReportedByName,
    Domain.Common.IncidentType IncidentType,
    int? Quantity,
    DateTime ReportedAt,
    string Description,
    string? ResolutionNotes,
    decimal? ReplacementCost,
    string? PhotoBlobUri);
