using Carbonate.Domain.Features.Boards;

namespace Carbonate.Application.Features.Boards;

internal static class BoardMapping
{
    public static BoardDto ToBoardDto(Board board, IReadOnlyList<CardRow> rows)
    {
        var cards = rows.ToLookup(row => row.Card.ColumnId);

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

    public static CardDto ToCardDto(CardRow row)
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
