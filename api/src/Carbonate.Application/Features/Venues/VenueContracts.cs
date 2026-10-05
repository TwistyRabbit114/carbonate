using Carbonate.Application.Common;
using Carbonate.Domain.Features.Venues;

namespace Carbonate.Application.Features.Venues;

//----------------------------------------------------------\\
//                              VENUES
//----------------------------------------------------------\\

//venues outlive events and get picked again for the next one (FR-32), so they're deactivated, never deleted.
//name and address are nullable here only so a blank one is reported by the validator together with every
//other field error, instead of alone by the framework's implicit required check
public record VenueRequest(
    string? Name,
    string? Address,
    string? AccessRoute,
    string? LoadingBayDetails,
    TimeOnly? OperatingHoursStart,
    TimeOnly? OperatingHoursEnd,
    bool RequiresSecurityClearance,
    bool RequiresHealthSafetyFile,
    string? PpeRequirements,
    bool IsActive = true);

public record VenueDto(
    Guid VenueId,
    string Name,
    string Address,
    string? AccessRoute,
    string? LoadingBayDetails,
    TimeOnly? OperatingHoursStart,
    TimeOnly? OperatingHoursEnd,
    bool RequiresSecurityClearance,
    bool RequiresHealthSafetyFile,
    string? PpeRequirements,
    bool IsActive);

//GET /api/venues?q=&includeInactive=&page=&pageSize=. no sort, the list is always by name
public sealed class VenueQuery
{
    public string? Q { get; init; }
    public bool IncludeInactive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageQuery.DefaultPageSize;
}

public interface IVenueService
{
    Task<PagedResult<VenueDto>> ListAsync(VenueQuery query, CancellationToken ct);
    Task<VenueDto> GetAsync(Guid venueId, CancellationToken ct);
    Task<VenueDto> CreateAsync(VenueRequest request, CancellationToken ct);
    Task<VenueDto> UpdateAsync(Guid venueId, VenueRequest request, CancellationToken ct);
}

//----------------------------------------------------------\\
//                              SITE VISITS
//----------------------------------------------------------\\

//the recce (FR-33). driver and crew names are personal information, they never go in a log
public record SiteVisitRequest(
    DateOnly VisitDate,
    Guid? ConductedByUserId, //left out means whoever is recording it
    string? VehicleType,
    string? LicencePlate,
    string? DriverName,
    string? RequiredDriverDetails,
    string? CrewNames,
    string? SignInProcedure,
    string? SecurityCheckpoint,
    string? HealthSafetyFileRef,
    string? Notes);

public record SiteVisitDto(
    Guid SiteVisitId,
    Guid EventId,
    UserRefDto ConductedBy,
    DateOnly VisitDate,
    string? VehicleType,
    string? LicencePlate,
    string? DriverName,
    string? RequiredDriverDetails,
    string? CrewNames,
    string? SignInProcedure,
    string? SecurityCheckpoint,
    string? HealthSafetyFileRef,
    string? Notes);

public interface ISiteVisitService
{
    Task<IReadOnlyList<SiteVisitDto>> ListForEventAsync(Guid eventId, CancellationToken ct);
    Task<SiteVisitDto> CreateAsync(Guid eventId, SiteVisitRequest request, CancellationToken ct);
    Task<SiteVisitDto> UpdateAsync(Guid siteVisitId, SiteVisitRequest request, CancellationToken ct);
}

//----------------------------------------------------------\\
//                              STORAGE
//----------------------------------------------------------\\

public record SiteVisitWithAuthor(SiteVisit Visit, string ConductedByName);

public interface IVenueRepository
{
    Task<PagedResult<Venue>> SearchAsync(string? text, bool includeInactive, int page, int pageSize, CancellationToken ct);
    Task<Venue?> FindAsync(Guid venueId, CancellationToken ct);

    /// <summary>The venue, only if the user is crewed on an active event held there.</summary>
    Task<Venue?> FindThroughAssignmentAsync(Guid venueId, Guid userId, CancellationToken ct);

    void Add(Venue venue);

    Task<IReadOnlyList<SiteVisitWithAuthor>> ListSiteVisitsAsync(Guid eventId, CancellationToken ct);

    /// <summary>The visit, tracked for changes, with the name of whoever conducted it.</summary>
    Task<SiteVisitWithAuthor?> FindSiteVisitAsync(Guid siteVisitId, CancellationToken ct);

    /// <summary>The user's name if they exist and are active, otherwise null.</summary>
    Task<string?> ActiveUserNameAsync(Guid userId, CancellationToken ct);

    void Add(SiteVisit visit);
    Task SaveChangesAsync(CancellationToken ct);
}
