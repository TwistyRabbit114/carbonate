using Carbonate.Application.Common;
using Carbonate.Application.Features.Boards;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Calendar;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Boards;

internal sealed class BoardRepository(CemDbContext db, TimeProvider clock) : IBoardRepository
{
    //----------------------------------------------------------\\
    //                              BOARDS
    //----------------------------------------------------------\\

    public Task<Board?> FindAdminBoardAsync(CancellationToken ct) =>
        db.Boards.AsNoTracking().Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.BoardType == BoardType.Admin, ct);

    public Task<Board?> FindEventBoardAsync(Guid eventId, CancellationToken ct) =>
        db.Boards.AsNoTracking().Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.EventId == eventId, ct);

    //----------------------------------------------------------\\
    //                              CARDS
    //----------------------------------------------------------\\

    public Task<IReadOnlyList<CardRow>> ListCardsAsync(Guid boardId, Guid? onlyAssignedTo, CancellationToken ct)
    {
        var cards = db.TaskCards.AsNoTracking()
            .Where(c => db.BoardColumns.Any(column => column.ColumnId == c.ColumnId && column.BoardId == boardId));
        if (onlyAssignedTo is { } userId)
        {
            cards = cards.Where(c => c.Assignments.Any(a => a.UserId == userId));
        }

        return ToRowsAsync(cards, ct);
    }

    public async Task<CardRow?> FindCardAsync(Guid cardId, CancellationToken ct) =>
        (await ToRowsAsync(db.TaskCards.AsNoTracking().Where(c => c.CardId == cardId), ct)).FirstOrDefault();

    public Task<CardPlace?> FindCardPlaceAsync(Guid cardId, CancellationToken ct) =>
        (
            from card in db.TaskCards
            join column in db.BoardColumns on card.ColumnId equals column.ColumnId
            join board in db.Boards on column.BoardId equals board.BoardId
            where card.CardId == cardId
            select new CardPlace(
                card.CardId,
                board.BoardId,
                board.BoardType,
                board.EventId,
                card.Assignments.Select(a => a.UserId).ToList()))
        .FirstOrDefaultAsync(ct);

    //----------------------------------------------------------\\
    //                              MOVES
    //----------------------------------------------------------\\

    public Task<Board?> LoadBoardForMoveAsync(Guid boardId, CancellationToken ct) =>
        db.Boards
            .Include(b => b.Columns).ThenInclude(column => column.Cards)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.BoardId == boardId, ct);

    //the update only lands if the row still has the version the client saw, so two people changing the
    //same card can't silently overwrite each other (NFR-15)
    public async Task<bool> TrySaveCardAsync(TaskCard card, byte[] expectedRowVersion, CancellationToken ct)
    {
        var entry = db.Entry(card);
        //a change of assignees only writes TASK_ASSIGNMENT rows, which would skip the check and leave the
        //card on its old version. marking the card changed means the update, and so the check, always runs
        if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }
        entry.Property(c => c.RowVersion).OriginalValue = expectedRowVersion;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    //----------------------------------------------------------\\
    //                              CREATE AND EDIT
    //----------------------------------------------------------\\

    public Task<Board?> FindBoardAsync(Guid boardId, CancellationToken ct) =>
        db.Boards.AsNoTracking().Include(b => b.Columns).FirstOrDefaultAsync(b => b.BoardId == boardId, ct);

    public Task<int> CountCardsAsync(Guid columnId, CancellationToken ct) =>
        db.TaskCards.CountAsync(c => c.ColumnId == columnId, ct);

    public void Add(TaskCard card) => db.TaskCards.Add(card);

    public Task<TaskCard?> LoadCardForEditAsync(Guid cardId, CancellationToken ct) =>
        db.TaskCards.Include(c => c.Assignments).FirstOrDefaultAsync(c => c.CardId == cardId, ct);

    public void AddAssignment(TaskAssignment assignment) => db.TaskAssignments.Add(assignment);

    public Task<bool> IsMilestoneOfEventAsync(Guid milestoneId, Guid eventId, CancellationToken ct) =>
        db.EventMilestones.AnyAsync(m => m.MilestoneId == milestoneId && m.EventId == eventId, ct);

    public async Task<IReadOnlyList<Guid>> MissingOrInactiveUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var active = await db.Users
            .Where(u => userIds.Contains(u.UserId) && u.IsActive)
            .Select(u => u.UserId)
            .ToListAsync(ct);
        return [.. userIds.Except(active)];
    }

    //the same rule as event visibility, asked about someone other than the caller
    public async Task<IReadOnlyList<Guid>> UsersWhoCannotSeeEventAsync(
        Guid eventId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var canSee = await db.Users
            .Where(u => userIds.Contains(u.UserId)
                && (u.UserRoles.Any(ur => ur.Role.RolePermissions.Any(rp => rp.Permission.Code == PermissionCodes.EventViewAll))
                    || db.CrewAssignments.Any(a => a.EventId == eventId && a.UserId == u.UserId)))
            .Select(u => u.UserId)
            .ToListAsync(ct);
        return [.. userIds.Except(canSee)];
    }

    //the calendar sync worker drains this, and checks for an existing link so an update never duplicates (FR-41)
    public void QueueCalendarPush(Guid cardId, bool remove)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        db.CalendarOutbox.Add(new CalendarOutbox
        {
            SourceEntityType = CalendarSourceType.TaskCard,
            SourceEntityId = cardId,
            Operation = remove ? OutboxOperation.Delete : OutboxOperation.Upsert,
            EnqueuedAt = now,
            NextAttemptAt = now,
        });
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    //----------------------------------------------------------\\
    //                              MAPPING
    //----------------------------------------------------------\\

    //one query for the cards and one for every name they mention, rather than a lookup per card
    private async Task<IReadOnlyList<CardRow>> ToRowsAsync(IQueryable<TaskCard> cards, CancellationToken ct)
    {
        var found = await cards
            .Select(c => new
            {
                Card = c,
                BoardId = db.BoardColumns.Where(column => column.ColumnId == c.ColumnId)
                    .Select(column => column.BoardId).First(),
                AssigneeIds = c.Assignments.OrderBy(a => a.AssignedAt).Select(a => a.UserId).ToList(),
                Attachments = c.Attachments.Count,
            })
            .ToListAsync(ct);

        var userIds = found
            .SelectMany(x => x.AssigneeIds.Append(x.Card.CreatedByUserId))
            .Concat(found.Select(x => x.Card.ReturnedByUserId).OfType<Guid>())
            .Distinct()
            .ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        UserRefDto Person(Guid userId) => new(userId, names.GetValueOrDefault(userId, ""));

        return [.. found.Select(x => new CardRow(
            x.Card,
            x.BoardId,
            [.. x.AssigneeIds.Select(Person)],
            Person(x.Card.CreatedByUserId),
            x.Card.ReturnedByUserId is { } returnedBy ? Person(returnedBy) : null,
            x.Attachments))];
    }
}
