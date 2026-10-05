using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;

namespace Carbonate.Application.Features.Boards;

//----------------------------------------------------------\\
//                              POSITIONS
//----------------------------------------------------------\\

public static class CardPositioner
{
    //boards are small, so both columns are simply renumbered 0..n on every move
    public static void Move(TaskCard card, BoardColumn from, BoardColumn to, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var source = from.Cards.Where(c => c.CardId != card.CardId).OrderBy(c => c.Position).ToList();
        var target = from.ColumnId == to.ColumnId
            ? source
            : to.Cards.Where(c => c.CardId != card.CardId).OrderBy(c => c.Position).ToList();

        //clamped, so a stale index from the client can't push the card off either end
        var index = Math.Clamp(targetIndex, 0, target.Count);
        target.Insert(index, card);
        card.ColumnId = to.ColumnId;

        Renumber(target);
        if (!ReferenceEquals(source, target))
        {
            Renumber(source);
        }
    }

    private static void Renumber(List<TaskCard> cards)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].Position = i;
        }
    }
}

//----------------------------------------------------------\\
//                              STATUS
//----------------------------------------------------------\\

//TASK_CARD.Status always follows the card's column (decision D-009)
public static class CardStatus
{
    public const string Open = "Open";
    public const string Done = "Done";
    public const string Assigned = "Assigned";
    public const string InProgressOrNeedsReview = "InProgressOrNeedsReview";
    public const string Complete = "Complete";

    //event boards only say whether a card is finished. the admin board names its three stages (FR-19)
    public static string For(BoardType boardType, BoardColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return boardType switch
        {
            BoardType.Admin when column.IsDoneColumn => Complete,
            BoardType.Admin when column.Position == 0 => Assigned,
            BoardType.Admin => InProgressOrNeedsReview,
            _ => column.IsDoneColumn ? Done : Open,
        };
    }
}
