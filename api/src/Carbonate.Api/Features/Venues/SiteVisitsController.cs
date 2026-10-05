using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Features.Venues;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Venues;

//----------------------------------------------------------\\
//                              SITE VISITS (FR-33)
//----------------------------------------------------------\\

public sealed class SiteVisitsController(ISiteVisitService siteVisits) : ApiControllerBase
{
    /// <summary>Anyone who can see the event, crew included. An event they can't see is a 404.</summary>
    [HttpGet("api/events/{eventId:guid}/site-visits")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<IReadOnlyList<SiteVisitDto>> List(Guid eventId, CancellationToken ct) =>
        siteVisits.ListForEventAsync(eventId, ct);

    //there's no single-visit read, the event's list is where visits are shown
    [HttpPost("api/events/{eventId:guid}/site-visits")]
    [HasPermission(PermissionCodes.VenueEdit)]
    public async Task<ActionResult<SiteVisitDto>> Create(Guid eventId, SiteVisitRequest request, CancellationToken ct)
    {
        var visit = await siteVisits.CreateAsync(eventId, request, ct);
        return StatusCode(StatusCodes.Status201Created, visit);
    }

    [HttpPut("api/site-visits/{siteVisitId:guid}")]
    [HasPermission(PermissionCodes.VenueEdit)]
    public Task<SiteVisitDto> Update(Guid siteVisitId, SiteVisitRequest request, CancellationToken ct) =>
        siteVisits.UpdateAsync(siteVisitId, request, ct);
}
