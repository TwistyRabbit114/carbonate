using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;

namespace Carbonate.Application.Features.Boards;

public sealed class BoardService(
    IBoardRepository boards,
    IEventAccess events,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider clock) : IBoardService
{
    //----------------------------------------------------------\\
    //                              READS
    //----------------------------------------------------------\\

    //desk roles see every admin task, crew only the ones assigned to them
    public async Task<BoardDto> GetAdminBoardAsync(CancellationToken ct)
    {
        if (!currentUser.HasPermission(PermissionCodes.AdminTaskView))
        {
            throw ProblemException.Forbidden();
        }

        var board = await boards.FindAdminBoardAsync(ct)
            ?? throw ProblemException.NotFound("The admin board hasn't been set up yet.");
        var onlyFor = currentUser.ScopeOf(PermissionCodes.AdminTaskView) == PermissionScope.All
            ? (Guid?)null
            : currentUser.UserId;

        return await ToBoardDtoAsync(board, onlyFor, ct);
    }

    //FR-21: crew see only the cards assigned to them, filtered in the query rather than after loading
    public async Task<BoardDto> GetEventBoardAsync(Guid eventId, CancellationToken ct)
    {
        if (!await events.CanSeeEventAsync(eventId, ct))
        {
            throw ProblemException.NotFound();
        }

        var board = await boards.FindEventBoardAsync(eventId, ct)
            ?? throw ProblemException.NotFound("This event has no task board.");
        var onlyFor = currentUser.HasPermission(PermissionCodes.EventViewAll) ? (Guid?)null : currentUser.UserId;

        return await ToBoardDtoAsync(board, onlyFor, ct);
    }

    public async Task<CardDto> GetCardAsync(Guid cardId, CancellationToken ct)
    {
        await RequireVisibleAsync(cardId, ct);
        return await LoadCardAsync(cardId, ct);
    }

    //----------------------------------------------------------\\
    //                              MOVES
    //----------------------------------------------------------\\

    //optimistic on the client (NFR-07). a stale rowVersion is a 409 carrying the card as it is now (NFR-15)
    public async Task<CardDto> MoveCardAsync(Guid cardId, MoveCardRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var place = await RequireVisibleAsync(cardId, ct);

        //on the admin board who may move what depends on who created and who holds the task (FR-20)
        if (place.BoardType == BoardType.Admin)
        {
            throw ProblemException.Conflict("/problems/invalid-transition", "Use the task's own actions",
                "Admin tasks move by submitting, returning or completing them.");
        }

        RequireMovePermission(place);

        var board = await boards.LoadBoardForMoveAsync(place.BoardId, ct) ?? throw ProblemException.NotFound();
        var from = board.Columns.Single(column => column.Cards.Any(c => c.CardId == cardId));
        var card = from.Cards.Single(c => c.CardId == cardId);
        var to = board.Columns.FirstOrDefault(column => column.ColumnId == request.ColumnId)
            ?? throw ProblemException.BusinessRule("That column isn't on this card's board.");

        var expected = Convert.FromBase64String(request.RowVersion ?? "");
        if (!expected.AsSpan().SequenceEqual(card.RowVersion))
        {
            throw await ConflictAsync(cardId, ct);
        }

        var before = new { from.ColumnId, card.Position, card.Status };
        CardPositioner.Move(card, from, to, request.Position);
        card.Status = CardStatus.For(board.BoardType, to);
        //keeps the original time when a done card is only reordered within the done column
        card.CompletedAt = to.IsDoneColumn ? card.CompletedAt ?? clock.GetUtcNow().UtcDateTime : null;

        if (!await boards.TrySaveMoveAsync(card, expected, ct))
        {
            throw await ConflictAsync(cardId, ct);
        }

        await audit.RecordAsync("card.moved", nameof(TaskCard), cardId.ToString(), before,
            new { to.ColumnId, card.Position, card.Status }, currentUser.UserId, ct);
        return await LoadCardAsync(cardId, ct);
    }

    //----------------------------------------------------------\\
    //                              CHECKS
    //----------------------------------------------------------\\

    //a card the caller can't see is a 404, one they can see but not change is a 403
    private async Task<CardPlace> RequireVisibleAsync(Guid cardId, CancellationToken ct)
    {
        var place = await boards.FindCardPlaceAsync(cardId, ct);
        if (place is null || !await CanSeeAsync(place, ct))
        {
            throw ProblemException.NotFound();
        }

        return place;
    }

    private async Task<bool> CanSeeAsync(CardPlace place, CancellationToken ct)
    {
        var assigned = place.AssigneeIds.Contains(currentUser.UserId);

        if (place.BoardType == BoardType.Admin)
        {
            return currentUser.HasPermission(PermissionCodes.AdminTaskView)
                && (assigned || currentUser.ScopeOf(PermissionCodes.AdminTaskView) == PermissionScope.All);
        }

        return place.EventId is { } eventId
            && await events.CanSeeEventAsync(eventId, ct)
            && (assigned || currentUser.HasPermission(PermissionCodes.EventViewAll));
    }

    //accounts can see event boards but has no task.move, and casual crew move only their own cards
    private void RequireMovePermission(CardPlace place)
    {
        var assignedOnly = currentUser.ScopeOf(PermissionCodes.TaskMove) != PermissionScope.All;
        if (!currentUser.HasPermission(PermissionCodes.TaskMove)
            || (assignedOnly && !place.AssigneeIds.Contains(currentUser.UserId)))
        {
            throw ProblemException.Forbidden();
        }
    }

    private async Task<ProblemException> ConflictAsync(Guid cardId, CancellationToken ct) =>
        ProblemException.Conflict("/problems/concurrency-conflict", "Changed by someone else",
            "Someone changed this card while you had it open. Reload to see the latest.",
            new Dictionary<string, object?> { ["current"] = await LoadCardAsync(cardId, ct) });

    //----------------------------------------------------------\\
    //                              MAPPING
    //----------------------------------------------------------\\

    private async Task<CardDto> LoadCardAsync(Guid cardId, CancellationToken ct) =>
        ToCardDto(await boards.FindCardAsync(cardId, ct) ?? throw ProblemException.NotFound());

    private async Task<BoardDto> ToBoardDtoAsync(Board board, Guid? onlyFor, CancellationToken ct)
    {
        var cards = (await boards.ListCardsAsync(board.BoardId, onlyFor, ct)).ToLookup(row => row.Card.ColumnId);

        var columns = board.Columns
            .OrderBy(column => column.Position)
            .Select(column => new ColumnDto(
                column.ColumnId,
                column.Name,
                column.Position,
                column.WipLimit,
                column.IsDoneColumn,
                [.. cards[column.ColumnId].OrderBy(row => row.Card.Position).Select(ToCardDto)]))
            .ToList();

        return new BoardDto(board.BoardId, board.BoardType, board.EventId, board.Name, columns);
    }

    private static CardDto ToCardDto(CardRow row)
    {
        var card = row.Card;
        return new CardDto(
            card.CardId,
            row.BoardId,
            card.ColumnId,
            card.Subject,
            card.Description,
            card.Priority,
            Utc(card.DueAt),
            card.Position,
            card.Status,
            card.MilestoneId,
            row.Assignees,
            row.CreatedBy,
            card.ReviewNotes,
            row.ReturnedBy,
            Utc(card.ReturnedAt),
            Utc(card.CompletedAt),
            row.AttachmentCount,
            Convert.ToBase64String(card.RowVersion));
    }

    //times are stored as utc but come back from the database unmarked, which would serialise without
    //the Z the plan asks for (section 5)
    private static DateTime? Utc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}
