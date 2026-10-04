using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Events;

/// <summary>Events, milestones and crew (FR-01 to FR-10). Stubs until the module lands; C owns the bodies.</summary>
[Route("api/events")]
public class EventsController : ApiControllerBase
{
    /// <summary>Events the caller may see. Crew see only events they are assigned to.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<PagedResult<EventListItem>> List([FromQuery] EventListQuery query) => NotYetBuilt();

    [HttpGet("{eventId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<EventDetail> Get(Guid eventId) => NotYetBuilt();

    /// <summary>Creates the event, its default milestone chain, its task board and its stock requirements.</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.EventCreate)]
    [ProducesResponseType<EventDetail>(StatusCodes.Status201Created)]
    public ActionResult<EventDetail> Create(SaveEventRequest request) => NotYetBuilt();

    [HttpPut("{eventId:guid}")]
    [HasPermission(PermissionCodes.EventEdit)]
    public ActionResult<EventDetail> Update(Guid eventId, UpdateEventRequest request) => NotYetBuilt();

    /// <summary>A soft delete, Director only.</summary>
    [HttpDelete("{eventId:guid}")]
    [HasPermission(PermissionCodes.EventDelete)]
    public IActionResult Delete(Guid eventId) => NotYetBuilt();

    [HttpGet("{eventId:guid}/milestones")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<IReadOnlyList<MilestoneDto>> Milestones(Guid eventId) => NotYetBuilt();

    /// <summary>Moves a milestone and cascades the change through everything that depends on it.</summary>
    [HttpPost("{eventId:guid}/milestones/{milestoneId:guid}/reschedule")]
    [HasPermission(PermissionCodes.EventEdit)]
    public ActionResult<ScheduleResultDto> Reschedule(Guid eventId, Guid milestoneId, RescheduleRequest request) =>
        NotYetBuilt();

    /// <summary>The event's crew. Hourly rates appear only for callers who may see them.</summary>
    [HttpGet("{eventId:guid}/crew")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<IReadOnlyList<CrewAssignmentDto>> Crew(Guid eventId) => NotYetBuilt();

    [HttpPost("{eventId:guid}/crew")]
    [HasPermission(PermissionCodes.CrewAssign)]
    [ProducesResponseType<CrewAssignmentDto>(StatusCodes.Status201Created)]
    public ActionResult<CrewAssignmentDto> AssignCrew(Guid eventId, AssignCrewRequest request) => NotYetBuilt();

    [HttpDelete("{eventId:guid}/crew/{assignmentId:guid}")]
    [HasPermission(PermissionCodes.CrewAssign)]
    public IActionResult RemoveCrew(Guid eventId, Guid assignmentId) => NotYetBuilt();

    /// <summary>Earlier events for the same client, to compare costs. Finance roles only.</summary>
    [HttpGet("{eventId:guid}/cost-history")]
    [HasPermission(PermissionCodes.FinanceViewClientPrice)]
    public ActionResult<IReadOnlyList<CostHistoryItem>> CostHistory(Guid eventId) => NotYetBuilt();

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

    /// <summary>Should have (FR-08).</summary>
    [HttpPatch("{eventId:guid}/pack-size")]
    [HasPermission(PermissionCodes.EventEdit)]
    public ActionResult<EventDetail> UpdatePackSize(Guid eventId, PackSizeRequest request) => NotYetBuilt();

    /// <summary>Which stages this event can move to now. The events board uses it to enable drop targets.</summary>
    [HttpGet("{eventId:guid}/allowed-transitions")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<AllowedTransitionsResponse> AllowedTransitions(Guid eventId) => NotYetBuilt();

    /// <summary>
    /// A manual stage move or a cancellation. Anything the state machine does not allow is a 409 with
    /// type /problems/invalid-transition.
    /// </summary>
    [HttpPost("{eventId:guid}/transitions")]
    [HasPermission(PermissionCodes.EventTransition)]
    public ActionResult<EventDetail> Transition(Guid eventId, TransitionRequest request) => NotYetBuilt();
}

public class UploadDocumentForm
{
    public IFormFile File { get; set; } = null!;
    public string DocumentType { get; set; } = "";
    public Carbonate.Domain.Common.SensitivityLevel SensitivityLevel { get; set; }
}
