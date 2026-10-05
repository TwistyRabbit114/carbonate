using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;

namespace Carbonate.Application.Features.Boards;

public sealed class BoardService(
    IBoardRepository boards,
    IEventAccess events,
    IHtmlSanitiser sanitiser,
    ICurrentUser currentUser,
    IAuditService audit,
    TimeProvider clock) : IBoardService
{
    private const int DescriptionLimit = 5000; //after cleaning, so markup that gets stripped doesn't count

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

        return BoardMapping.ToBoardDto(board, await boards.ListCardsAsync(board.BoardId, onlyFor, ct));
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

        return BoardMapping.ToBoardDto(board, await boards.ListCardsAsync(board.BoardId, onlyFor, ct));
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

        RequireScoped(PermissionCodes.TaskMove, place);

        var board = await boards.LoadBoardForMoveAsync(place.BoardId, ct) ?? throw ProblemException.NotFound();
        var from = board.Columns.Single(column => column.Cards.Any(c => c.CardId == cardId));
        var card = from.Cards.Single(c => c.CardId == cardId);
        var to = board.Columns.FirstOrDefault(column => column.ColumnId == request.ColumnId)
            ?? throw ProblemException.BusinessRule("That column isn't on this card's board.");

        var expected = await RequireRowVersionAsync(card, request.RowVersion, ct);

        var before = new { from.ColumnId, card.Position, card.Status };
        CardPositioner.Move(card, from, to, request.Position);
        card.Status = CardStatus.For(board.BoardType, to);
        //keeps the original time when a done card is only reordered within the done column
        card.CompletedAt = to.IsDoneColumn ? card.CompletedAt ?? clock.GetUtcNow().UtcDateTime : null;

        if (!await boards.TrySaveCardAsync(card, expected, ct))
        {
            throw await ConflictAsync(cardId, ct);
        }

        await audit.RecordAsync("card.moved", nameof(TaskCard), cardId.ToString(), before,
            new { to.ColumnId, card.Position, card.Status }, currentUser.UserId, ct);
        return await LoadCardAsync(cardId, ct);
    }

    //----------------------------------------------------------\\
    //                              CREATE AND EDIT
    //----------------------------------------------------------\\

    public async Task<CardDto> CreateCardAsync(Guid boardId, CreateCardRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var board = await boards.FindBoardAsync(boardId, ct) ?? throw ProblemException.NotFound();
        await RequireBoardVisibleAsync(board, ct);

        var assignees = Distinct(request.AssigneeIds);
        var column = board.BoardType == BoardType.Admin
            ? await PrepareAdminTaskAsync(board, request, assignees, ct)
            : await PrepareEventCardAsync(board, request, assignees, ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var card = new TaskCard
        {
            ColumnId = column.ColumnId,
            Subject = request.Subject?.Trim() ?? "",
            Description = Clean(request.Description),
            Priority = request.Priority ?? CardPriority.Normal,
            DueAt = request.DueAt?.UtcDateTime,
            MilestoneId = request.MilestoneId,
            Position = await boards.CountCardsAsync(column.ColumnId, ct),
            Status = CardStatus.For(board.BoardType, column),
            CompletedAt = column.IsDoneColumn ? now : null,
            //for an admin task this is the manager who later completes or returns it (FR-20)
            CreatedByUserId = currentUser.UserId,
            CreatedAt = now,
        };
        foreach (var userId in assignees)
        {
            card.Assignments.Add(new TaskAssignment { UserId = userId, AssignedAt = now });
        }

        boards.Add(card);
        if (card.DueAt is not null)
        {
            boards.QueueCalendarPush(card.CardId, remove: false);
        }
        await boards.SaveChangesAsync(ct);

        var created = await LoadCardAsync(card.CardId, ct);
        await audit.RecordAsync("card.created", nameof(TaskCard), card.CardId.ToString(), null, created,
            currentUser.UserId, ct);
        return created;
    }

    public async Task<CardDto> UpdateCardAsync(Guid cardId, UpdateCardRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var place = await RequireVisibleAsync(cardId, ct);
        RequireScoped(place.BoardType == BoardType.Admin ? PermissionCodes.AdminTaskEdit : PermissionCodes.TaskEdit,
            place);
        await RequireMilestoneAsync(place.BoardType, place.EventId, request.MilestoneId, ct);

        var card = await boards.LoadCardForEditAsync(cardId, ct) ?? throw ProblemException.NotFound();
        var expected = await RequireRowVersionAsync(card, request.RowVersion, ct);
        var before = await LoadCardAsync(cardId, ct);

        var subject = request.Subject?.Trim() ?? "";
        var dueAt = request.DueAt?.UtcDateTime;
        QueueCalendarChange(card, subject, dueAt);

        card.Subject = subject;
        card.Description = Clean(request.Description);
        card.Priority = request.Priority ?? CardPriority.Normal;
        card.DueAt = dueAt;
        card.MilestoneId = request.MilestoneId;

        if (!await boards.TrySaveCardAsync(card, expected, ct))
        {
            throw await ConflictAsync(cardId, ct);
        }

        var after = await LoadCardAsync(cardId, ct);
        await audit.RecordAsync("card.updated", nameof(TaskCard), cardId.ToString(), before, after,
            currentUser.UserId, ct);
        return after;
    }

    //----------------------------------------------------------\\
    //                              ASSIGNEES
    //----------------------------------------------------------\\

    //admin tasks are handed out by the managers who review them; event cards by whoever can edit them
    public async Task<CardDto> SetAssigneesAsync(Guid cardId, AssigneesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var place = await RequireVisibleAsync(cardId, ct);
        var assignees = Distinct(request.UserIds);

        if (place.BoardType == BoardType.Admin)
        {
            Require(PermissionCodes.AdminTaskAssign);
            RequireSomeone(assignees);
            await RequireActiveUsersAsync(assignees, ct);
        }
        else
        {
            RequireScoped(PermissionCodes.TaskEdit, place);
            await RequireActiveUsersAsync(assignees, ct);
            await RequireOnEventAsync(place.EventId!.Value, assignees, ct);
        }

        var card = await boards.LoadCardForEditAsync(cardId, ct) ?? throw ProblemException.NotFound();
        var before = card.Assignments.Select(a => a.UserId).Order().ToList();

        //anyone dropped is deleted with the save, anyone new gets their own row; people kept keep theirs
        card.Assignments.RemoveAll(a => !assignees.Contains(a.UserId));
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var userId in assignees.Where(id => card.Assignments.TrueForAll(a => a.UserId != id)))
        {
            boards.AddAssignment(new TaskAssignment { CardId = cardId, UserId = userId, AssignedAt = now });
        }
        await boards.SaveChangesAsync(ct);

        await audit.RecordAsync("card.assignees_changed", nameof(TaskCard), cardId.ToString(),
            new { UserIds = before }, new { UserIds = assignees.Order().ToList() }, currentUser.UserId, ct);
        return await LoadCardAsync(cardId, ct);
    }

    //----------------------------------------------------------\\
    //                              PREPARING A NEW CARD
    //----------------------------------------------------------\\

    private async Task<BoardColumn> PrepareAdminTaskAsync(
        Board board, CreateCardRequest request, IReadOnlyList<Guid> assignees, CancellationToken ct)
    {
        Require(PermissionCodes.AdminTaskAssign);

        var assigned = board.Columns.Single(column => column.Position == 0);
        if (request.ColumnId is { } columnId && columnId != assigned.ColumnId)
        {
            throw ProblemException.BusinessRule("New admin tasks start in Assigned.");
        }

        await RequireMilestoneAsync(BoardType.Admin, null, request.MilestoneId, ct);
        RequireSomeone(assignees);
        await RequireActiveUsersAsync(assignees, ct);
        return assigned;
    }

    //casual crew only act on cards already assigned to them, so adding a card takes full task.edit
    private async Task<BoardColumn> PrepareEventCardAsync(
        Board board, CreateCardRequest request, IReadOnlyList<Guid> assignees, CancellationToken ct)
    {
        if (currentUser.ScopeOf(PermissionCodes.TaskEdit) != PermissionScope.All)
        {
            throw ProblemException.Forbidden();
        }

        var column = request.ColumnId is { } columnId
            ? board.Columns.FirstOrDefault(c => c.ColumnId == columnId)
                ?? throw ProblemException.BusinessRule("That column isn't on this board.")
            : board.Columns.MinBy(c => c.Position)!;

        await RequireMilestoneAsync(BoardType.Event, board.EventId, request.MilestoneId, ct);
        await RequireActiveUsersAsync(assignees, ct);
        await RequireOnEventAsync(board.EventId!.Value, assignees, ct);
        return column;
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

    private async Task RequireBoardVisibleAsync(Board board, CancellationToken ct)
    {
        var visible = board.BoardType == BoardType.Admin
            ? currentUser.HasPermission(PermissionCodes.AdminTaskView)
            : board.EventId is { } eventId && await events.CanSeeEventAsync(eventId, ct);
        if (!visible)
        {
            throw ProblemException.NotFound();
        }
    }

    private void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }

    //"asg" in the plan's matrix: the permission only reaches cards assigned to the caller
    private void RequireScoped(string permission, CardPlace place)
    {
        var assignedOnly = currentUser.ScopeOf(permission) != PermissionScope.All;
        if (!currentUser.HasPermission(permission)
            || (assignedOnly && !place.AssigneeIds.Contains(currentUser.UserId)))
        {
            throw ProblemException.Forbidden();
        }
    }

    private async Task<byte[]> RequireRowVersionAsync(TaskCard card, string? rowVersion, CancellationToken ct)
    {
        var expected = Convert.FromBase64String(rowVersion ?? "");
        if (!expected.AsSpan().SequenceEqual(card.RowVersion))
        {
            throw await ConflictAsync(card.CardId, ct);
        }

        return expected;
    }

    private async Task RequireMilestoneAsync(BoardType boardType, Guid? eventId, Guid? milestoneId, CancellationToken ct)
    {
        if (milestoneId is not { } id)
        {
            return;
        }
        if (boardType == BoardType.Admin)
        {
            throw ProblemException.BusinessRule("Admin tasks aren't tied to an event milestone.");
        }
        if (!await boards.IsMilestoneOfEventAsync(id, eventId!.Value, ct))
        {
            throw ProblemException.BusinessRule("That milestone belongs to a different event.");
        }
    }

    private static void RequireSomeone(IReadOnlyList<Guid> assignees)
    {
        if (assignees.Count == 0)
        {
            throw FieldError("assigneeIds", "Choose who should do this task.");
        }
    }

    private async Task RequireActiveUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count > 0 && (await boards.MissingOrInactiveUsersAsync(userIds, ct)).Count > 0)
        {
            throw FieldError("assigneeIds", "Pick people who have an active Carbonate account.");
        }
    }

    //a card is only any use to someone who can open the event it belongs to
    private async Task RequireOnEventAsync(Guid eventId, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count > 0 && (await boards.UsersWhoCannotSeeEventAsync(eventId, userIds, ct)).Count > 0)
        {
            throw ProblemException.BusinessRule("Add them to the event's crew first, then assign the card.");
        }
    }

    private async Task<ProblemException> ConflictAsync(Guid cardId, CancellationToken ct) =>
        ProblemException.Conflict("/problems/concurrency-conflict", "Changed by someone else",
            "Someone changed this card while you had it open. Reload to see the latest.",
            new Dictionary<string, object?> { ["current"] = await LoadCardAsync(cardId, ct) });

    private static ProblemException FieldError(string field, string message) =>
        ProblemException.Validation(new Dictionary<string, string[]> { [field] = [message] });

    //----------------------------------------------------------\\
    //                              HELPERS
    //----------------------------------------------------------\\

    //google gets the new date or title, or the entry comes off the calendar when the date is cleared
    private void QueueCalendarChange(TaskCard card, string subject, DateTime? dueAt)
    {
        if (dueAt is not null && (dueAt != card.DueAt || subject != card.Subject))
        {
            boards.QueueCalendarPush(card.CardId, remove: false);
        }
        else if (dueAt is null && card.DueAt is not null)
        {
            boards.QueueCalendarPush(card.CardId, remove: true);
        }
    }

    private string? Clean(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var clean = sanitiser.Sanitise(description);
        if (clean.Length > DescriptionLimit)
        {
            throw FieldError("description", "Keep the description under 5000 characters.");
        }

        return clean.Length == 0 ? null : clean;
    }

    private static List<Guid> Distinct(IReadOnlyList<Guid>? ids) => ids?.Distinct().ToList() ?? [];

    private async Task<CardDto> LoadCardAsync(Guid cardId, CancellationToken ct) =>
        BoardMapping.ToCardDto(await boards.FindCardAsync(cardId, ct) ?? throw ProblemException.NotFound());
}
