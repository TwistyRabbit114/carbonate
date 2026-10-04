using Carbonate.Application.Features.Stock;
using Shouldly;

namespace Carbonate.UnitTests.Features.Stock;

/// <summary>FR-27 and FR-29. The two warnings that stop a crew finding out at doors.</summary>
public class StockRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 1);
    private static readonly DateTime LoadIn = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);

    // ---- FR-27, shortfall ----------------------------------------------------------------

    [Fact]
    public void Warns_when_less_is_planned_than_the_guests_will_drink()
    {
        // 45 kg of ice per hundred guests, 600 guests: 270 expected, 200 planned.
        var warning = StockRules.Shortfall(200m, 45m, 600, LoadIn, Now);

        warning.ShouldNotBeNull();
        warning.Code.ShouldBe(StockWarningCodes.Shortfall);
        warning.Details["expected"].ShouldBe(270m);
        warning.Details["planned"].ShouldBe(200m);
    }

    [Fact]
    public void Says_nothing_when_exactly_enough_is_planned() =>
        StockRules.Shortfall(270m, 45m, 600, LoadIn, Now).ShouldBeNull();

    [Fact]
    public void Says_nothing_when_more_than_enough_is_planned() =>
        StockRules.Shortfall(300m, 45m, 600, LoadIn, Now).ShouldBeNull();

    [Fact]
    public void Says_nothing_about_an_item_with_no_consumption_figure()
    {
        // A mobile bar is not consumed. Warning on every unmeasured item would train people to
        // ignore the warnings.
        StockRules.Shortfall(1m, null, 600, LoadIn, Now).ShouldBeNull();
    }

    [Fact]
    public void Stops_warning_once_load_in_has_passed()
    {
        // The truck has gone. A warning nobody can act on only hides the ones they can.
        StockRules.Shortfall(200m, 45m, 600, LoadIn, LoadIn.AddHours(1)).ShouldBeNull();
    }

    [Fact]
    public void Still_warns_right_up_to_load_in() =>
        StockRules.Shortfall(200m, 45m, 600, LoadIn, LoadIn.AddMinutes(-1)).ShouldNotBeNull();

    [Fact]
    public void Warns_for_an_event_with_no_load_in_milestone_yet() =>
        StockRules.Shortfall(200m, 45m, 600, null, Now).ShouldNotBeNull();

    [Fact]
    public void Says_nothing_when_nothing_is_planned_and_no_guests_are_expected() =>
        StockRules.Shortfall(0m, 45m, 0, LoadIn, Now).ShouldBeNull();

    [Theory]
    // Expected scales with the pack size, so the same plan is fine for one event and short for another.
    [InlineData(100, 45, 45, false)]
    [InlineData(200, 45, 45, true)]
    public void Expected_consumption_scales_with_the_pack_size(
        int packSize, decimal perHundred, decimal planned, bool shouldWarn) =>
        (StockRules.Shortfall(planned, perHundred, packSize, LoadIn, Now) is not null).ShouldBe(shouldWarn);

    // ---- FR-29, lead time ----------------------------------------------------------------

    [Fact]
    public void Warns_when_the_supplier_cannot_deliver_in_time()
    {
        // Coastal Ice needs 5 days; the stock is needed in 3.
        var warning = StockRules.LeadTime(Today.AddDays(3), 5, Today);

        warning.ShouldNotBeNull();
        warning.Code.ShouldBe(StockWarningCodes.LeadTime);
        warning.Details["leadTimeDays"].ShouldBe(5);
    }

    [Fact]
    public void Says_nothing_when_the_order_would_arrive_with_time_to_spare() =>
        StockRules.LeadTime(Today.AddDays(10), 5, Today).ShouldBeNull();

    [Fact]
    public void Says_nothing_when_it_would_arrive_exactly_on_the_day()
    {
        // Arriving on the required day is on time, not late.
        StockRules.LeadTime(Today.AddDays(5), 5, Today).ShouldBeNull();
    }

    [Fact]
    public void Warns_by_a_single_day() =>
        StockRules.LeadTime(Today.AddDays(4), 5, Today).ShouldNotBeNull();

    [Fact]
    public void Says_nothing_for_an_item_with_no_supplier()
    {
        // No supplier is its own problem, reported as "unassigned supplier" on the order list (FR-28).
        StockRules.LeadTime(Today.AddDays(1), null, Today).ShouldBeNull();
    }

    [Fact]
    public void Handles_a_same_day_supplier() =>
        StockRules.LeadTime(Today, 0, Today).ShouldBeNull();

    [Fact]
    public void Warns_about_a_date_that_has_already_passed() =>
        StockRules.LeadTime(Today.AddDays(-1), 2, Today).ShouldNotBeNull();
}
