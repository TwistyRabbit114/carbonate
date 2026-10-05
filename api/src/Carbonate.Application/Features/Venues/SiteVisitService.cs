using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Venues;

namespace Carbonate.Application.Features.Venues;

public sealed class SiteVisitService(
    IVenueRepository venues,
    IEventAccess events,
    ICurrentUser currentUser,
    IAuditService audit) : ISiteVisitService
{
    //----------------------------------------------------------\\
    //                              READS
    //----------------------------------------------------------\\

    //anyone who can see the event sees its recce, crew included: it's where sign-in and access live
    public async Task<IReadOnlyList<SiteVisitDto>> ListForEventAsync(Guid eventId, CancellationToken ct)
    {
        await RequireEventAsync(eventId, ct);

        var visits = await venues.ListSiteVisitsAsync(eventId, ct);
        return [.. visits.Select(row => ToDto(row.Visit, row.ConductedByName))];
    }

    //----------------------------------------------------------\\
    //                              WRITES
    //----------------------------------------------------------\\

    public async Task<SiteVisitDto> CreateAsync(Guid eventId, SiteVisitRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireVenueEdit();
        await RequireEventAsync(eventId, ct);

        var conductedBy = request.ConductedByUserId ?? currentUser.UserId;
        var conductedByName = await ConductorNameAsync(conductedBy, ct);

        var visit = new SiteVisit { EventId = eventId };
        Apply(visit, request, conductedBy);
        venues.Add(visit);
        await venues.SaveChangesAsync(ct);

        var created = ToDto(visit, conductedByName);
        await audit.RecordAsync("site_visit.created", nameof(SiteVisit), visit.SiteVisitId.ToString(), null, created,
            currentUser.UserId, ct);
        return created;
    }

    public async Task<SiteVisitDto> UpdateAsync(Guid siteVisitId, SiteVisitRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireVenueEdit();

        var found = await venues.FindSiteVisitAsync(siteVisitId, ct) ?? throw ProblemException.NotFound();
        var visit = found.Visit;
        await RequireEventAsync(visit.EventId, ct);

        var before = ToDto(visit, found.ConductedByName);

        //only a new name is checked. casual crew are deactivated a week after debrief, and that
        //mustn't stop anyone correcting a recce one of them did
        var conductedBy = request.ConductedByUserId ?? visit.ConductedByUserId;
        var conductedByName = conductedBy == visit.ConductedByUserId
            ? found.ConductedByName
            : await ConductorNameAsync(conductedBy, ct);

        Apply(visit, request, conductedBy);
        await venues.SaveChangesAsync(ct);

        var after = ToDto(visit, conductedByName);
        await audit.RecordAsync("site_visit.updated", nameof(SiteVisit), visit.SiteVisitId.ToString(), before, after,
            currentUser.UserId, ct);
        return after;
    }

    //----------------------------------------------------------\\
    //                              CHECKS
    //----------------------------------------------------------\\

    //an event the caller can't see, or one that was deleted, reads as not found (plan section 7.2)
    private async Task RequireEventAsync(Guid eventId, CancellationToken ct)
    {
        if (!await events.CanSeeEventAsync(eventId, ct))
        {
            throw ProblemException.NotFound();
        }
    }

    private void RequireVenueEdit()
    {
        if (!currentUser.HasPermission(PermissionCodes.VenueEdit))
        {
            throw ProblemException.Forbidden();
        }
    }

    private async Task<string> ConductorNameAsync(Guid userId, CancellationToken ct) =>
        await venues.ActiveUserNameAsync(userId, ct)
        ?? throw ProblemException.Validation(new Dictionary<string, string[]>
        {
            ["conductedByUserId"] = ["Pick someone who has an active Carbonate account."],
        });

    //----------------------------------------------------------\\
    //                              MAPPING
    //----------------------------------------------------------\\

    private static void Apply(SiteVisit visit, SiteVisitRequest request, Guid conductedBy)
    {
        visit.ConductedByUserId = conductedBy;
        visit.VisitDate = request.VisitDate;
        visit.VehicleType = FieldText.Clean(request.VehicleType);
        visit.LicencePlate = FieldText.Clean(request.LicencePlate)?.ToUpperInvariant();
        visit.DriverName = FieldText.Clean(request.DriverName);
        visit.RequiredDriverDetails = FieldText.Clean(request.RequiredDriverDetails);
        visit.CrewNames = FieldText.Clean(request.CrewNames);
        visit.SignInProcedure = FieldText.Clean(request.SignInProcedure);
        visit.SecurityCheckpoint = FieldText.Clean(request.SecurityCheckpoint);
        visit.HealthSafetyFileRef = FieldText.Clean(request.HealthSafetyFileRef);
        visit.Notes = FieldText.Clean(request.Notes);
    }

    private static SiteVisitDto ToDto(SiteVisit visit, string conductedByName) => new(
        visit.SiteVisitId,
        visit.EventId,
        new UserRefDto(visit.ConductedByUserId, conductedByName),
        visit.VisitDate,
        visit.VehicleType,
        visit.LicencePlate,
        visit.DriverName,
        visit.RequiredDriverDetails,
        visit.CrewNames,
        visit.SignInProcedure,
        visit.SecurityCheckpoint,
        visit.HealthSafetyFileRef,
        visit.Notes);
}
