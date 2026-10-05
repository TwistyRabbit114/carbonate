using Carbonate.Application.Common;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Incidents;

/// <summary>FR-31. Breakages, equipment failures and stock shortfalls, reported from the floor.</summary>
public interface IIncidentService
{
    /// <summary>
    /// Records an incident with an optional photo.
    /// </summary>
    /// <param name="photo">
    /// Null when none was attached. The stream is validated and stored by <c>IFileStorage</c>; nothing
    /// about it is trusted.
    /// </param>
    Task<IncidentDto> ReportAsync(Guid eventId, ReportIncident report, IncidentPhoto? photo, CancellationToken ct);

    Task<IReadOnlyList<IncidentDto>> ForEventAsync(Guid eventId, CancellationToken ct);

    Task<PagedResult<IncidentDto>> ListAsync(IncidentListQuery query, CancellationToken ct);

    Task<IncidentDto> UpdateAsync(Guid incidentId, UpdateIncidentRequest request, CancellationToken ct);
}

/// <param name="AssetId">The serialised asset this happened to, if it was one.</param>
/// <param name="StockItemId">The catalogue item, if it was not a serialised asset.</param>
public sealed record ReportIncident(
    IncidentType IncidentType,
    Guid? AssetId,
    Guid? StockItemId,
    int? Quantity,
    string Description);

/// <summary>
/// An uploaded photo, kept free of any web type so the service layer does not depend on ASP.NET.
/// </summary>
public sealed record IncidentPhoto(Stream Content, string FileName, string ContentType);
