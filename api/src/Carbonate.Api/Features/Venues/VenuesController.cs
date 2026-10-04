using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Venues;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Venues;

/// <summary>Venues and site visits (FR-32, FR-33). Stubs until the module lands; B owns the bodies.</summary>
[ApiController]
public class VenuesController : ApiControllerBase
{
    [HttpGet("api/venues")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<PagedResult<VenueDto>> List([FromQuery] VenueListQuery query) => NotYetBuilt();

    [HttpPost("api/venues")]
    [HasPermission(PermissionCodes.VenueEdit)]
    [ProducesResponseType<VenueDto>(StatusCodes.Status201Created)]
    public ActionResult<VenueDto> Create(SaveVenueRequest request) => NotYetBuilt();

    [HttpGet("api/venues/{venueId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<VenueDto> Get(Guid venueId) => NotYetBuilt();

    [HttpPut("api/venues/{venueId:guid}")]
    [HasPermission(PermissionCodes.VenueEdit)]
    public ActionResult<VenueDto> Update(Guid venueId, SaveVenueRequest request) => NotYetBuilt();

    [HttpGet("api/events/{eventId:guid}/site-visits")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<IReadOnlyList<SiteVisitDto>> SiteVisits(Guid eventId) => NotYetBuilt();

    [HttpPost("api/events/{eventId:guid}/site-visits")]
    [HasPermission(PermissionCodes.VenueEdit)]
    [ProducesResponseType<SiteVisitDto>(StatusCodes.Status201Created)]
    public ActionResult<SiteVisitDto> CreateSiteVisit(Guid eventId, SaveSiteVisitRequest request) => NotYetBuilt();

    [HttpPut("api/site-visits/{siteVisitId:guid}")]
    [HasPermission(PermissionCodes.VenueEdit)]
    public ActionResult<SiteVisitDto> UpdateSiteVisit(Guid siteVisitId, SaveSiteVisitRequest request) => NotYetBuilt();
}
