using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Features.Boards;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Boards;

//----------------------------------------------------------\\
//                              BOARDS (FR-19, FR-21)
//----------------------------------------------------------\\

//not the events board (FR-18): that one is the event list itself and has no board table behind it
public sealed class BoardsController(IBoardService boards) : ApiControllerBase
{
    /// <summary>The single Admin Tasks board. Crew get only the cards assigned to them.</summary>
    [HttpGet("api/boards/admin")]
    [HasPermission(PermissionCodes.AdminTaskView)]
    public Task<BoardDto> GetAdminBoard(CancellationToken ct) => boards.GetAdminBoardAsync(ct);

    /// <summary>An event's task board. An event the caller can't see is a 404; crew get only their cards.</summary>
    [HttpGet("api/events/{eventId:guid}/board")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<BoardDto> GetEventBoard(Guid eventId, CancellationToken ct) => boards.GetEventBoardAsync(eventId, ct);
}

//----------------------------------------------------------\\
//                              CARDS
//----------------------------------------------------------\\

//a card can sit on either board and the two have different rules, so the attribute is the gate every
//role passes and the service applies the board's own permission (task.move, admin_task.view)
[Route("api/cards")]
public sealed class CardsController(IBoardService boards) : ApiControllerBase
{
    [HttpGet("{cardId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<CardDto> Get(Guid cardId, CancellationToken ct) => boards.GetCardAsync(cardId, ct);

    /// <summary>
    /// Adds a card to the bottom of a column. On the admin board it takes admin_task.assign and always
    /// starts in Assigned; on an event board it takes task.edit.
    /// </summary>
    [HttpPost("~/api/boards/{boardId:guid}/cards")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public async Task<ActionResult<CardDto>> Create(Guid boardId, CreateCardRequest request, CancellationToken ct)
    {
        var card = await boards.CreateCardAsync(boardId, request, ct);
        return CreatedAtAction(nameof(Get), new { cardId = card.CardId }, card);
    }

    /// <summary>Replaces the card's editable fields, so send them all. Needs the card's rowVersion.</summary>
    [HttpPatch("{cardId:guid}")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<CardDto> Update(Guid cardId, UpdateCardRequest request, CancellationToken ct) =>
        boards.UpdateCardAsync(cardId, request, ct);

    /// <summary>Replaces who the card is assigned to. Needs the card's rowVersion, and the card gets a new one.</summary>
    [HttpPut("{cardId:guid}/assignees")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<CardDto> SetAssignees(Guid cardId, AssigneesRequest request, CancellationToken ct) =>
        boards.SetAssigneesAsync(cardId, request, ct);

    /// <summary>Signs off a handed-in admin task. Only the manager who created it, never an assignee (FR-20).</summary>
    [HttpPost("{cardId:guid}/complete")]
    [HasPermission(PermissionCodes.AdminTaskReview)]
    public Task<CardDto> Complete(Guid cardId, CompleteTaskRequest request, CancellationToken ct) =>
        boards.CompleteTaskAsync(cardId, request, ct);

    /// <summary>Sends a handed-in admin task back to Assigned with notes. Creator only (FR-20).</summary>
    [HttpPost("{cardId:guid}/return")]
    [HasPermission(PermissionCodes.AdminTaskReview)]
    public Task<CardDto> Return(Guid cardId, ReturnTaskRequest request, CancellationToken ct) =>
        boards.ReturnTaskAsync(cardId, request, ct);

    /// <summary>Moves a card to a column and position. A stale rowVersion is a 409 with the current card.</summary>
    [HttpPost("{cardId:guid}/move")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<CardDto> Move(Guid cardId, MoveCardRequest request, CancellationToken ct) =>
        boards.MoveCardAsync(cardId, request, ct);

    /// <summary>Files and photos on a card (FR-22, a Should). In the contract, not built yet.</summary>
    [HttpGet("{cardId:guid}/attachments")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public ActionResult<IReadOnlyList<CardAttachmentDto>> Attachments(Guid cardId) => NotYetBuilt();

    [HttpPost("{cardId:guid}/attachments")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<CardAttachmentDto>(StatusCodes.Status201Created)]
    public ActionResult<CardAttachmentDto> Attach(Guid cardId, [FromForm] AttachmentForm form) => NotYetBuilt();
}

public sealed class AttachmentForm
{
    public IFormFile File { get; set; } = null!;
}
