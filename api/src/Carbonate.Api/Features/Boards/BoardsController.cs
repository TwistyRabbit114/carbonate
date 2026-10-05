using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Features.Boards;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Boards;

//----------------------------------------------------------\\
//                              BOARDS (FR-19, FR-21)
//----------------------------------------------------------\\

//not the events board (FR-18): that one is the event list itself and has no board table behind it
[ApiController]
public sealed class BoardsController(IBoardService boards) : ControllerBase
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
[ApiController]
[Route("api/cards")]
public sealed class CardsController(IBoardService boards) : ControllerBase
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

    /// <summary>Replaces who the card is assigned to.</summary>
    [HttpPut("{cardId:guid}/assignees")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<CardDto> SetAssignees(Guid cardId, AssigneesRequest request, CancellationToken ct) =>
        boards.SetAssigneesAsync(cardId, request, ct);

    /// <summary>Moves a card to a column and position. A stale rowVersion is a 409 with the current card.</summary>
    [HttpPost("{cardId:guid}/move")]
    [HasPermission(PermissionCodes.EventViewAssigned)]
    public Task<CardDto> Move(Guid cardId, MoveCardRequest request, CancellationToken ct) =>
        boards.MoveCardAsync(cardId, request, ct);
}
