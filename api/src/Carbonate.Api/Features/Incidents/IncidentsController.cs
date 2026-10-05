using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Incidents;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Incidents;

/// <summary>Incident reports (FR-31). Crew can report only against events they are assigned to.</summary>
[ApiController]
public class IncidentsController(IIncidentService incidents) : ApiControllerBase
{
    /// <summary>Quick entry from a phone: breakage, equipment failure or stock shortfall, with an optional photo.</summary>
    [HttpPost("api/events/{eventId:guid}/incidents")]
    [HasPermission(PermissionCodes.IncidentCreate)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IncidentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<IncidentDto>> Report(
        Guid eventId, [FromForm] ReportIncidentForm form, CancellationToken ct)
    {
        var report = new ReportIncident(
            form.IncidentType, form.AssetId, form.StockItemId, form.Quantity, form.Description);

        // The stream is opened here and closed when the request ends; the service layer never sees
        // IFormFile, so it stays free of ASP.NET types.
        await using var content = form.Photo?.OpenReadStream();
        var photo = form.Photo is null || content is null
            ? null
            : new IncidentPhoto(content, form.Photo.FileName, form.Photo.ContentType);

        var created = await incidents.ReportAsync(eventId, report, photo, ct);
        return CreatedAtAction(nameof(ForEvent), new { eventId }, created);
    }

    [HttpGet("api/events/{eventId:guid}/incidents")]
    [HasPermission(PermissionCodes.IncidentView)]
    public async Task<ActionResult<IReadOnlyList<IncidentDto>>> ForEvent(Guid eventId, CancellationToken ct) =>
        Ok(await incidents.ForEventAsync(eventId, ct));

    [HttpGet("api/incidents")]
    [HasPermission(PermissionCodes.IncidentView)]
    public async Task<ActionResult<PagedResult<IncidentDto>>> List(
        [FromQuery] IncidentListQuery query, CancellationToken ct) =>
        Ok(await incidents.ListAsync(query, ct));

    /// <summary>Adds resolution notes and the replacement cost.</summary>
    [HttpPatch("api/incidents/{incidentId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public async Task<ActionResult<IncidentDto>> Update(
        Guid incidentId, UpdateIncidentRequest request, CancellationToken ct) =>
        Ok(await incidents.UpdateAsync(incidentId, request, ct));
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
