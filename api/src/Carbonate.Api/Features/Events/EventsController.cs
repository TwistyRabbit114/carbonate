using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Events;

/// <summary>Events, milestones, crew and stage moves (FR-01 to FR-10).</summary>
[Route("api/events")]
public class EventsController(IEventService events, ICommercialService commercial) : ApiControllerBase
{
    /// <summary>Events the caller may see. Crew see only events they are assigned to.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public async Task<ActionResult<PagedResult<EventListItem>>> List([FromQuery] EventListQuery query, CancellationToken ct) =>
        Ok(await events.ListAsync(query, ct));

    [HttpGet("{eventId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public async Task<ActionResult<EventDetail>> Get(Guid eventId, CancellationToken ct) =>
        Ok(await events.GetAsync(eventId, ct));

    /// <summary>Creates the event, its default milestone chain, its task board and its stock requirements.</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.EventCreate)]
    [ProducesResponseType<EventDetail>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EventDetail>> Create(SaveEventRequest request, CancellationToken ct)
    {
        var created = await events.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { eventId = created.EventId }, created);
    }

    [HttpPut("{eventId:guid}")]
    [HasPermission(PermissionCodes.EventEdit)]
    public async Task<ActionResult<EventDetail>> Update(Guid eventId, UpdateEventRequest request, CancellationToken ct) =>
        Ok(await events.UpdateAsync(eventId, request, ct));

    /// <summary>A soft delete, Director only.</summary>
    [HttpDelete("{eventId:guid}")]
    [HasPermission(PermissionCodes.EventDelete)]
    public async Task<IActionResult> Delete(Guid eventId, CancellationToken ct)
    {
        await events.DeleteAsync(eventId, ct);
        return NoContent();
    }

    [HttpGet("{eventId:guid}/milestones")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public async Task<ActionResult<IReadOnlyList<MilestoneDto>>> Milestones(Guid eventId, CancellationToken ct) =>
        Ok(await events.GetMilestonesAsync(eventId, ct));

    /// <summary>Moves a milestone and cascades the change through everything that depends on it.</summary>
    [HttpPost("{eventId:guid}/milestones/{milestoneId:guid}/reschedule")]
    [HasPermission(PermissionCodes.EventEdit)]
    public async Task<ActionResult<ScheduleResultDto>> Reschedule(
        Guid eventId, Guid milestoneId, RescheduleRequest request, CancellationToken ct) =>
        Ok(await events.RescheduleAsync(eventId, milestoneId, request, ct));

    /// <summary>The event's crew. Hourly rates appear only for callers who may see them.</summary>
    [HttpGet("{eventId:guid}/crew")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public async Task<ActionResult<IReadOnlyList<CrewAssignmentDto>>> Crew(Guid eventId, CancellationToken ct) =>
        Ok(await events.GetCrewAsync(eventId, ct));

    [HttpPost("{eventId:guid}/crew")]
    [HasPermission(PermissionCodes.CrewAssign)]
    [ProducesResponseType<CrewAssignmentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CrewAssignmentDto>> AssignCrew(Guid eventId, AssignCrewRequest request, CancellationToken ct)
    {
        var assignment = await events.AssignCrewAsync(eventId, request, ct);
        return StatusCode(StatusCodes.Status201Created, assignment);
    }

    [HttpDelete("{eventId:guid}/crew/{assignmentId:guid}")]
    [HasPermission(PermissionCodes.CrewAssign)]
    public async Task<IActionResult> RemoveCrew(Guid eventId, Guid assignmentId, CancellationToken ct)
    {
        await events.RemoveCrewAsync(eventId, assignmentId, ct);
        return NoContent();
    }

    /// <summary>Earlier events for the same client, to compare costs. Finance roles only.</summary>
    [HttpGet("{eventId:guid}/cost-history")]
    [HasPermission(PermissionCodes.FinanceViewClientPrice)]
    public async Task<ActionResult<IReadOnlyList<CostHistoryItem>>> CostHistory(Guid eventId, CancellationToken ct) =>
        Ok(await commercial.GetCostHistoryAsync(eventId, ct));

    /// <summary>Should have (FR-05).</summary>
    [HttpPut("{eventId:guid}/service-schedule")]
    [HasPermission(PermissionCodes.EventEdit)]
    public ActionResult<EventDetail> SaveServiceSchedule(Guid eventId, ServiceScheduleRequest request) => NotYetBuilt();

    /// <summary>Should have (FR-06). Confidential documents need document.view_confidential.</summary>
    [HttpGet("{eventId:guid}/documents")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<IReadOnlyList<EventDocumentDto>> Documents(Guid eventId) => NotYetBuilt();

    [HttpPost("{eventId:guid}/documents")]
    [HasPermission(PermissionCodes.DocumentUpload)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<EventDocumentDto>(StatusCodes.Status201Created)]
    public ActionResult<EventDocumentDto> UploadDocument(
        Guid eventId, [FromForm] UploadDocumentForm form) => NotYetBuilt();

    /// <summary>Records the estimated and, after the event, the actual pack size. Only what is sent changes (FR-08).</summary>
    [HttpPatch("{eventId:guid}/pack-size")]
    [HasPermission(PermissionCodes.EventEdit)]
    public async Task<ActionResult<EventDetail>> UpdatePackSize(Guid eventId, PackSizeRequest request, CancellationToken ct) =>
        Ok(await events.UpdatePackSizeAsync(eventId, request, ct));

    /// <summary>Which stages this event can move to now. The events board uses it to enable drop targets.</summary>
    [HttpGet("{eventId:guid}/allowed-transitions")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public async Task<ActionResult<AllowedTransitionsResponse>> AllowedTransitions(Guid eventId, CancellationToken ct) =>
        Ok(await events.GetAllowedTransitionsAsync(eventId, ct));

    /// <summary>
    /// A manual stage move or a cancellation. Anything the state machine does not allow is a 409 with
    /// type /problems/invalid-transition.
    /// </summary>
    [HttpPost("{eventId:guid}/transitions")]
    [HasPermission(PermissionCodes.EventTransition)]
    public async Task<ActionResult<EventDetail>> Transition(Guid eventId, TransitionRequest request, CancellationToken ct) =>
        Ok(await events.TransitionAsync(eventId, request, ct));
}

public class UploadDocumentForm
{
    public IFormFile File { get; set; } = null!;
    public string DocumentType { get; set; } = "";
    public Carbonate.Domain.Common.SensitivityLevel SensitivityLevel { get; set; }
}

/// <summary>Lookups the event form and crew picker read (FR-01, FR-07).</summary>
[ApiController]
public class EventLookupsController(IEventService events) : ApiControllerBase
{
    /// <summary>Active clients, for anyone who can create or edit events.</summary>
    [HttpGet("api/clients")]
    [HasPermission(PermissionCodes.EventCreate)]
    public async Task<ActionResult<PagedResult<ClientOption>>> Clients([FromQuery] ClientListQuery query, CancellationToken ct) =>
        Ok(await events.ListClientsAsync(query, ct));

    [HttpGet("api/divisions")]
    [HasPermission(PermissionCodes.EventCreate)]
    public async Task<ActionResult<IReadOnlyList<DivisionOption>>> Divisions(CancellationToken ct) =>
        Ok(await events.ListDivisionsAsync(ct));

    /// <summary>Active people who can be assigned to a crew: name and roles only.</summary>
    [HttpGet("api/crew/candidates")]
    [HasPermission(PermissionCodes.CrewAssign)]
    public async Task<ActionResult<IReadOnlyList<CrewCandidate>>> CrewCandidates(CancellationToken ct) =>
        Ok(await events.ListCrewCandidatesAsync(ct));
}
