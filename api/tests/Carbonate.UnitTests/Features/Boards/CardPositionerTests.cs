using Carbonate.Application.Features.Boards;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Shouldly;

namespace Carbonate.UnitTests.Features.Boards;

//card moves renumber both columns 0..n, so positions never gap or repeat however the client got its index
public class CardPositionerTests
{
    private static BoardColumn Column(int position, params string[] subjects)
    {
        var column = new BoardColumn { Position = position };
        for (var i = 0; i < subjects.Length; i++)
        {
            column.Cards.Add(new TaskCard { Subject = subjects[i], ColumnId = column.ColumnId, Position = i });
        }
        return column;
    }

    private static string[] Order(BoardColumn column) =>
        [.. column.Cards.OrderBy(c => c.Position).Select(c => c.Subject)];

    private static TaskCard Card(BoardColumn column, string subject) => column.Cards.Single(c => c.Subject == subject);

    //----------------------------------------------------------\\
    //                              WITHIN A COLUMN
    //----------------------------------------------------------\\

    [Fact]
    public void Moves_a_card_up_within_its_column()
    {
        var column = Column(0, "A", "B", "C", "D");

        CardPositioner.Move(Card(column, "D"), column, column, 1);

        Order(column).ShouldBe(["A", "D", "B", "C"]);
        column.Cards.Select(c => c.Position).Order().ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public void Moves_a_card_down_within_its_column()
    {
        var column = Column(0, "A", "B", "C", "D");

        CardPositioner.Move(Card(column, "A"), column, column, 2);

        Order(column).ShouldBe(["B", "C", "A", "D"]);
    }

    //----------------------------------------------------------\\
    //                              ACROSS COLUMNS
    //----------------------------------------------------------\\

    [Fact]
    public void Moves_a_card_across_and_closes_the_gap_it_leaves()
    {
        var todo = Column(0, "A", "B", "C");
        var done = Column(1, "X", "Y");
        var card = Card(todo, "B");

        CardPositioner.Move(card, todo, done, 1);
        //ef moves the card between the two collections on save, done by hand here
        todo.Cards.Remove(card);
        done.Cards.Add(card);

        card.ColumnId.ShouldBe(done.ColumnId);
        Order(todo).ShouldBe(["A", "C"]);
        Order(done).ShouldBe(["X", "B", "Y"]);
        todo.Cards.Select(c => c.Position).Order().ShouldBe([0, 1]);
    }

    [Theory]
    [InlineData(99, new[] { "X", "Y", "A" })]
    [InlineData(-5, new[] { "A", "X", "Y" })]
    public void Clamps_an_index_off_either_end(int index, string[] expected)
    {
        var todo = Column(0, "A");
        var done = Column(1, "X", "Y");
        var card = Card(todo, "A");

        CardPositioner.Move(card, todo, done, index);
        done.Cards.Add(card);

        Order(done).ShouldBe(expected);
    }

    [Fact]
    public void Moves_into_an_empty_column()
    {
        var todo = Column(0, "A", "B");
        var done = Column(1);
        var card = Card(todo, "A");

        CardPositioner.Move(card, todo, done, 0);

        card.Position.ShouldBe(0);
        Card(todo, "B").Position.ShouldBe(0);
    }

    //----------------------------------------------------------\\
    //                              STATUS
    //----------------------------------------------------------\\

    [Theory]
    [InlineData(0, false, "Open")]
    [InlineData(1, false, "Open")]
    [InlineData(2, true, "Done")]
    public void Event_cards_are_open_or_done(int position, bool isDone, string expected) =>
        CardStatus.For(BoardType.Event, new BoardColumn { Position = position, IsDoneColumn = isDone })
            .ShouldBe(expected);

    [Theory]
    [InlineData(0, false, "Assigned")]
    [InlineData(1, false, "InProgressOrNeedsReview")]
    [InlineData(2, true, "Complete")]
    public void Admin_cards_take_their_column_s_stage(int position, bool isDone, string expected) =>
        CardStatus.For(BoardType.Admin, new BoardColumn { Position = position, IsDoneColumn = isDone })
            .ShouldBe(expected);
}
