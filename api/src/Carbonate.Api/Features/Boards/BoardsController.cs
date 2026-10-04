using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Features.Boards;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Boards;

/// <summary>
/// Event task boards and the admin tasks board (FR-19 to FR-23). Stubs until the module lands; B owns
/// the bodies. Crew roles list and act only on cards assigned to them (FR-21).
/// </summary>
[ApiController]
public class BoardsController : ApiControllerBase
{
    [HttpGet("api/boards/admin")]
    [HasPermission(PermissionCodes.AdminTaskView)]
    public ActionResult<BoardDto> Admin() => NotYetBuilt();

    [HttpGet("api/events/{eventId:guid}/board")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<BoardDto> ForEvent(Guid eventId) => NotYetBuilt();

    [HttpPost("api/boards/{boardId:guid}/cards")]
    [HasPermission(PermissionCodes.TaskEdit)]
    [ProducesResponseType<CardDto>(StatusCodes.Status201Created)]
    public ActionResult<CardDto> CreateCard(Guid boardId, CreateCardRequest request) => NotYetBuilt();

    [HttpGet("api/cards/{cardId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<CardDto> GetCard(Guid cardId) => NotYetBuilt();

    [HttpPatch("api/cards/{cardId:guid}")]
    [HasPermission(PermissionCodes.TaskEdit)]
    public ActionResult<CardDto> UpdateCard(Guid cardId, UpdateCardRequest request) => NotYetBuilt();

    /// <summary>Checks the row version and the WIP limit, and renumbers the column, in one transaction.</summary>
    [HttpPost("api/cards/{cardId:guid}/move")]
    [HasPermission(PermissionCodes.TaskMove)]
    public ActionResult<CardDto> MoveCard(Guid cardId, MoveCardRequest request) => NotYetBuilt();

    [HttpPut("api/cards/{cardId:guid}/assignees")]
    [HasPermission(PermissionCodes.TaskEdit)]
    public ActionResult<CardDto> SetAssignees(Guid cardId, AssigneesRequest request) => NotYetBuilt();

    /// <summary>Only the manager who created an admin task can complete it (FR-20).</summary>
    [HttpPost("api/cards/{cardId:guid}/complete")]
    [HasPermission(PermissionCodes.AdminTaskReview)]
    public ActionResult<CardDto> Complete(Guid cardId, ReviewCardRequest request) => NotYetBuilt();

    /// <summary>Sends an admin task back to Assigned with review notes (FR-20).</summary>
    [HttpPost("api/cards/{cardId:guid}/return")]
    [HasPermission(PermissionCodes.AdminTaskReview)]
    public ActionResult<CardDto> Return(Guid cardId, ReviewCardRequest request) => NotYetBuilt();

    /// <summary>Should have (FR-22).</summary>
    [HttpGet("api/cards/{cardId:guid}/attachments")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<IReadOnlyList<CardAttachmentDto>> Attachments(Guid cardId) => NotYetBuilt();

    [HttpPost("api/cards/{cardId:guid}/attachments")]
    [HasPermission(PermissionCodes.TaskEdit)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CardAttachmentDto>(StatusCodes.Status201Created)]
    public ActionResult<CardAttachmentDto> Attach(Guid cardId, [FromForm] AttachmentForm form) => NotYetBuilt();
}

public class AttachmentForm
{
    public IFormFile File { get; set; } = null!;
}
