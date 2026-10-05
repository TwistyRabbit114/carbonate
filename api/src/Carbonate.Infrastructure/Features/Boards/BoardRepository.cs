using Carbonate.Application.Common;
using Carbonate.Application.Features.Boards;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Boards;

internal sealed class BoardRepository(CemDbContext db) : IBoardRepository
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

    //the update only lands if the row still has the version the client saw, so two people moving the
    //same card can't silently overwrite each other (NFR-15)
    public async Task<bool> TrySaveMoveAsync(TaskCard card, byte[] expectedRowVersion, CancellationToken ct)
    {
        db.Entry(card).Property(c => c.RowVersion).OriginalValue = expectedRowVersion;
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
