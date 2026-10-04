using Carbonate.Domain.Features.Events;
using Shouldly;

namespace Carbonate.UnitTests.Events;

public class ScheduleCalculatorTests
{
    private static readonly DateTime Day = new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime At(double hours) => Day.AddHours(hours);

    private static ScheduleItem Item(string name, double start, double end, DateTime? actualStart = null) =>
        new(Id(name), At(start), At(end), actualStart);

    private static Guid Id(string name) => new(name.PadLeft(32, '0').GetHashCode().ToString("x8").PadLeft(32, '0'));

    private static ScheduleLink Link(string from, string to, double lagHours = 0) =>
        new(Id(from), Id(to), TimeSpan.FromHours(lagHours));

    private static ScheduleItem Find(ScheduleResult result, string name) => result.Items.Single(i => i.Id == Id(name));

    /// <summary>A -> B -> C, each two hours with no gap.</summary>
    private static (ScheduleItem[] Items, ScheduleLink[] Links) Chain() =>
        ([Item("a", 0, 2), Item("b", 2, 4), Item("c", 4, 6)], [Link("a", "b"), Link("b", "c")]);

    [Fact]
    public void Moving_the_first_milestone_of_a_chain_shifts_everything_after_it()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Id("a"), At(3), At(5));

        result.Succeeded.ShouldBeTrue();
        Find(result, "a").Start.ShouldBe(At(3));
        Find(result, "b").Start.ShouldBe(At(5));
        Find(result, "b").End.ShouldBe(At(7));
        Find(result, "c").Start.ShouldBe(At(7));
        result.Moved.Count.ShouldBe(3);
    }

    [Fact]
    public void Moving_the_middle_of_a_chain_leaves_what_comes_before_untouched()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Id("b"), At(6), At(8));

        Find(result, "a").Start.ShouldBe(At(0));
        Find(result, "b").Start.ShouldBe(At(6));
        Find(result, "c").Start.ShouldBe(At(8));
        result.Moved.ShouldNotContain(Id("a"));
    }

    [Fact]
    public void A_successor_keeps_its_own_duration_when_the_moved_milestone_changes_length()
    {
        var items = new[] { Item("a", 0, 2), Item("b", 2, 6) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "b")], Id("a"), At(0), At(5));

        Find(result, "b").Start.ShouldBe(At(5));
        Find(result, "b").Duration().ShouldBe(TimeSpan.FromHours(4));
    }

    [Fact]
    public void Gaps_between_milestones_are_kept_when_they_shift_together()
    {
        var items = new[] { Item("a", 0, 2), Item("b", 10, 12) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "b")], Id("a"), At(1), At(3));

        Find(result, "b").Start.ShouldBe(At(11));
    }

    [Fact]
    public void A_diamond_moves_both_branches_and_the_join_lands_after_the_later_one()
    {
        // a -> b -> d and a -> c -> d, where c is the longer branch.
        var items = new[] { Item("a", 0, 1), Item("b", 1, 2), Item("c", 1, 5), Item("d", 5, 6) };
        var links = new[] { Link("a", "b"), Link("a", "c"), Link("b", "d"), Link("c", "d") };

        var result = ScheduleCalculator.Recalculate(items, links, Id("a"), At(2), At(3));

        Find(result, "b").Start.ShouldBe(At(3));
        Find(result, "c").End.ShouldBe(At(7));
        Find(result, "d").Start.ShouldBe(At(7));
    }

    [Fact]
    public void A_milestone_with_two_predecessors_is_pushed_back_by_the_one_that_was_not_moved()
    {
        // x and y both feed z. Pulling x earlier would drag z earlier too, but y still ends at 6.
        var items = new[] { Item("x", 4, 6), Item("y", 0, 6), Item("z", 8, 10) };
        var links = new[] { Link("x", "z"), Link("y", "z") };

        var result = ScheduleCalculator.Recalculate(items, links, Id("x"), At(0), At(2));

        Find(result, "z").Start.ShouldBe(At(6));
        Find(result, "z").End.ShouldBe(At(8));
        Find(result, "y").Start.ShouldBe(At(0));
    }

    [Fact]
    public void A_multi_predecessor_milestone_stays_put_when_the_other_branch_still_blocks_it()
    {
        var items = new[] { Item("x", 0, 2), Item("y", 0, 6), Item("z", 6, 8) };
        var links = new[] { Link("x", "z"), Link("y", "z") };

        var result = ScheduleCalculator.Recalculate(items, links, Id("x"), At(1), At(3));

        Find(result, "z").Start.ShouldBe(At(7));
    }

    [Fact]
    public void Pulling_a_milestone_earlier_pulls_its_successors_with_it()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Id("a"), At(-1), At(1));

        Find(result, "a").Start.ShouldBe(At(-1));
        Find(result, "b").Start.ShouldBe(At(1));
        Find(result, "c").Start.ShouldBe(At(3));
        Find(result, "c").End.ShouldBe(At(5));
    }

    [Fact]
    public void Pulling_a_milestone_earlier_than_its_predecessor_allows_pushes_it_back_to_the_limit()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Id("b"), At(0), At(2));

        // a ends at 2, so b cannot start before 2: it is held there and c is not pulled earlier.
        Find(result, "b").Start.ShouldBe(At(2));
        Find(result, "b").End.ShouldBe(At(4));
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Lag_is_respected_between_a_predecessor_and_its_successor()
    {
        var items = new[] { Item("a", 0, 2), Item("b", 3, 5) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "b", lagHours: 1)], Id("a"), At(2), At(4));

        Find(result, "b").Start.ShouldBe(At(5));
    }

    [Fact]
    public void Milestones_with_no_dependency_on_the_moved_one_do_not_move()
    {
        var items = new[] { Item("a", 0, 2), Item("b", 2, 4), Item("loner", 20, 22) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "b")], Id("a"), At(5), At(7));

        Find(result, "loner").Start.ShouldBe(At(20));
        result.Moved.ShouldNotContain(Id("loner"));
    }

    [Fact]
    public void Moving_something_to_where_it_already_is_changes_nothing()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Id("b"), At(2), At(4));

        result.Succeeded.ShouldBeTrue();
        result.Moved.ShouldBeEmpty();
    }

    [Fact]
    public void A_milestone_that_has_started_cannot_be_moved()
    {
        var items = new[] { Item("a", 0, 2, actualStart: At(0.1)), Item("b", 2, 4) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "b")], Id("a"), At(1), At(3));

        result.Failure.ShouldBe(ScheduleFailure.FrozenMilestone);
        result.Moved.ShouldBeEmpty();
    }

    [Fact]
    public void A_cascade_that_would_move_a_started_milestone_is_refused_as_a_whole()
    {
        var items = new[] { Item("a", 0, 2), Item("b", 2, 4, actualStart: At(2)), Item("c", 4, 6) };
        var links = new[] { Link("a", "b"), Link("b", "c") };

        var result = ScheduleCalculator.Recalculate(items, links, Id("a"), At(1), At(3));

        result.Failure.ShouldBe(ScheduleFailure.FrozenMilestone);
        result.Items.ShouldBe(items);
    }

    [Fact]
    public void A_started_milestone_that_the_change_does_not_reach_is_fine()
    {
        var items = new[] { Item("done", 0, 2, actualStart: At(0)), Item("a", 4, 6), Item("b", 6, 8) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "b")], Id("a"), At(5), At(7));

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void A_dependency_loop_is_refused()
    {
        var items = new[] { Item("a", 0, 2), Item("b", 2, 4), Item("c", 4, 6) };
        var links = new[] { Link("a", "b"), Link("b", "c"), Link("c", "a") };

        var result = ScheduleCalculator.Recalculate(items, links, Id("a"), At(1), At(3));

        result.Failure.ShouldBe(ScheduleFailure.Cycle);
    }

    [Fact]
    public void A_milestone_cannot_depend_on_itself()
    {
        var items = new[] { Item("a", 0, 2) };

        var result = ScheduleCalculator.Recalculate(items, [Link("a", "a")], Id("a"), At(1), At(3));

        result.Failure.ShouldBe(ScheduleFailure.Cycle);
    }

    [Fact]
    public void A_window_that_ends_before_it_starts_is_refused()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Id("a"), At(5), At(4));

        result.Failure.ShouldBe(ScheduleFailure.InvalidWindow);
    }

    [Fact]
    public void An_unknown_milestone_is_refused()
    {
        var (items, links) = Chain();

        var result = ScheduleCalculator.Recalculate(items, links, Guid.NewGuid(), At(1), At(2));

        result.Failure.ShouldBe(ScheduleFailure.UnknownMilestone);
    }

    [Fact]
    public void A_dependency_on_a_milestone_outside_the_event_is_refused()
    {
        var items = new[] { Item("a", 0, 2) };

        var result = ScheduleCalculator.Recalculate(items, [new ScheduleLink(Id("a"), Guid.NewGuid(), TimeSpan.Zero)],
            Id("a"), At(1), At(3));

        result.Failure.ShouldBe(ScheduleFailure.UnknownMilestone);
    }

    [Fact]
    public void The_inputs_are_never_changed()
    {
        var (items, links) = Chain();
        var before = items.ToList();

        ScheduleCalculator.Recalculate(items, links, Id("a"), At(3), At(5));

        items.ShouldBe(before);
    }
}

internal static class ScheduleItemExtensions
{
    public static TimeSpan Duration(this ScheduleItem item) => item.End - item.Start;
}
