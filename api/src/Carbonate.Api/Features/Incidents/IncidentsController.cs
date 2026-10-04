using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Incidents;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Incidents;

/// <summary>Incident reports (FR-31). Stubs until built; D owns the bodies. Crew can report only against assigned events.</summary>
[ApiController]
public class IncidentsController : ApiControllerBase
{
    /// <summary>Quick entry from a phone: breakage, equipment failure or stock shortfall, with an optional photo.</summary>
    [HttpPost("api/events/{eventId:guid}/incidents")]
    [HasPermission(PermissionCodes.IncidentCreate)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status201Created)]
    public ActionResult<IncidentDto> Report(Guid eventId, [FromForm] ReportIncidentForm form) => NotYetBuilt();

    [HttpGet("api/events/{eventId:guid}/incidents")]
    [HasPermission(PermissionCodes.IncidentView)]
    public ActionResult<IReadOnlyList<IncidentDto>> ForEvent(Guid eventId) => NotYetBuilt();

    [HttpGet("api/incidents")]
    [HasPermission(PermissionCodes.IncidentView)]
    public ActionResult<PagedResult<IncidentDto>> List([FromQuery] IncidentListQuery query) => NotYetBuilt();

    /// <summary>Adds resolution notes and the replacement cost.</summary>
    [HttpPatch("api/incidents/{incidentId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public ActionResult<IncidentDto> Update(Guid incidentId, UpdateIncidentRequest request) => NotYetBuilt();
}

/// <summary>Sent as multipart form data. At least one of AssetId and StockItemId is required.</summary>
public class ReportIncidentForm
{
    public Carbonate.Domain.Common.IncidentType IncidentType { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? StockItemId { get; set; }
    public int? Quantity { get; set; }
    public string Description { get; set; } = "";
    public IFormFile? Photo { get; set; }
}
