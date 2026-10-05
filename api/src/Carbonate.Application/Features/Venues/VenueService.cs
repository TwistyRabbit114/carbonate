using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Venues;

namespace Carbonate.Application.Features.Venues;

public sealed class VenueService(IVenueRepository venues, ICurrentUser currentUser, IAuditService audit) : IVenueService
{
    //----------------------------------------------------------\\
    //                              READS
    //----------------------------------------------------------\\

    //the whole list is for desk roles. crew only ever reach a venue through an event they're on (US-23)
    public async Task<PagedResult<VenueDto>> ListAsync(VenueQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!currentUser.HasPermission(PermissionCodes.EventViewAll))
        {
            throw ProblemException.Forbidden();
        }

        var page = await venues.SearchAsync(FieldText.Clean(query.Q), query.IncludeInactive, query.Page, query.PageSize, ct);
        return new PagedResult<VenueDto>
        {
            Items = [.. page.Items.Select(ToDto)],
            Page = page.Page,
            PageSize = page.PageSize,
            Total = page.Total,
        };
    }

    //a venue crew aren't working at is a 404, not a 403, so they can't tell which venues exist
    public async Task<VenueDto> GetAsync(Guid venueId, CancellationToken ct)
    {
        var venue = currentUser.HasPermission(PermissionCodes.EventViewAll)
            ? await venues.FindAsync(venueId, ct)
            : await venues.FindThroughAssignmentAsync(venueId, currentUser.UserId, ct);

        return venue is null ? throw ProblemException.NotFound() : ToDto(venue);
    }

    //----------------------------------------------------------\\
    //                              WRITES
    //----------------------------------------------------------\\

    public async Task<VenueDto> CreateAsync(VenueRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireVenueEdit();

        var venue = new Venue();
        Apply(venue, request);
        venues.Add(venue);
        await venues.SaveChangesAsync(ct);

        var created = ToDto(venue);
        await audit.RecordAsync("venue.created", nameof(Venue), venue.VenueId.ToString(), null, created,
            currentUser.UserId, ct);
        return created;
    }

    //deactivating is an update with isActive false, so the venue's history stays on old events
    public async Task<VenueDto> UpdateAsync(Guid venueId, VenueRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireVenueEdit();

        var venue = await venues.FindAsync(venueId, ct) ?? throw ProblemException.NotFound();
        var before = ToDto(venue);
        Apply(venue, request);
        await venues.SaveChangesAsync(ct);

        var after = ToDto(venue);
        await audit.RecordAsync("venue.updated", nameof(Venue), venue.VenueId.ToString(), before, after,
            currentUser.UserId, ct);
        return after;
    }

    //----------------------------------------------------------\\
    //                              MAPPING
    //----------------------------------------------------------\\

    private void RequireVenueEdit()
    {
        if (!currentUser.HasPermission(PermissionCodes.VenueEdit))
        {
            throw ProblemException.Forbidden();
        }
    }

    private static void Apply(Venue venue, VenueRequest request)
    {
        venue.Name = request.Name?.Trim() ?? "";
        venue.Address = request.Address?.Trim() ?? "";
        venue.AccessRoute = FieldText.Clean(request.AccessRoute);
        venue.LoadingBayDetails = FieldText.Clean(request.LoadingBayDetails);
        venue.OperatingHoursStart = request.OperatingHoursStart;
        venue.OperatingHoursEnd = request.OperatingHoursEnd;
        venue.RequiresSecurityClearance = request.RequiresSecurityClearance;
        venue.RequiresHealthSafetyFile = request.RequiresHealthSafetyFile;
        venue.PpeRequirements = FieldText.Clean(request.PpeRequirements);
        venue.IsActive = request.IsActive;
    }

    private static VenueDto ToDto(Venue venue) => new(
        venue.VenueId,
        venue.Name,
        venue.Address,
        venue.AccessRoute,
        venue.LoadingBayDetails,
        venue.OperatingHoursStart,
        venue.OperatingHoursEnd,
        venue.RequiresSecurityClearance,
        venue.RequiresHealthSafetyFile,
        venue.PpeRequirements,
        venue.IsActive);
}
