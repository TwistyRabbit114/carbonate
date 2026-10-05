using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Venues;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Venues;

//----------------------------------------------------------\\
//                              VENUES (FR-32)
//----------------------------------------------------------\\

[Route("api/venues")]
public sealed class VenuesController(IVenueService venues) : ApiControllerBase
{
    /// <summary>Searchable list for the event form and settings. Desk roles only.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.EventViewAll)]
    public Task<PagedResult<VenueDto>> List([FromQuery] VenueQuery query, CancellationToken ct) =>
        venues.ListAsync(query, ct);

    /// <summary>Crew get a venue only through an event they're assigned to; the service checks.</summary>
    [HttpGet("{venueId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<VenueDto> Get(Guid venueId, CancellationToken ct) => venues.GetAsync(venueId, ct);

    [HttpPost]
    [HasPermission(PermissionCodes.VenueEdit)]
    public async Task<ActionResult<VenueDto>> Create(VenueRequest request, CancellationToken ct)
    {
        var venue = await venues.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { venueId = venue.VenueId }, venue);
    }

    /// <summary>Also how a venue is deactivated: send isActive false. Venues are never deleted.</summary>
    [HttpPut("{venueId:guid}")]
    [HasPermission(PermissionCodes.VenueEdit)]
    public Task<VenueDto> Update(Guid venueId, VenueRequest request, CancellationToken ct) =>
        venues.UpdateAsync(venueId, request, ct);
}
