using Carbonate.Application.Features.Commercial;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Commercial;
using Shouldly;

namespace Carbonate.UnitTests.Commercial;

public class QuoteCalculatorTests
{
    private const decimal Vat = 0.15m;

    [Fact]
    public void A_simple_quote_adds_up_with_vat_cost_and_margin()
    {
        QuoteLineInput[] lines =
        [
            new(10, 40m, 100m),
            new(2, 250m, 400m),
        ];

        var totals = QuoteCalculator.Calculate(lines, Vat);

        totals.LineTotals.ShouldBe([1000m, 800m]);
        totals.SubtotalExVat.ShouldBe(1800m);
        totals.VatAmount.ShouldBe(270m);
        totals.TotalIncVat.ShouldBe(2070m);
        totals.InternalCostTotal.ShouldBe(900m);
        totals.MarginPercent.ShouldBe(50m);
    }

    [Fact]
    public void Each_line_is_rounded_to_the_cent_so_the_lines_always_add_up_to_the_subtotal()
    {
        // 3 x 3.335 is 10.005, which rounds away from zero to 10.01 per line.
        QuoteLineInput[] lines = [new(3, 1m, 3.335m), new(3, 1m, 3.335m)];

        var totals = QuoteCalculator.Calculate(lines, Vat);

        totals.LineTotals.ShouldBe([10.01m, 10.01m]);
        totals.SubtotalExVat.ShouldBe(totals.LineTotals.Sum());
        totals.SubtotalExVat.ShouldBe(20.02m);
    }

    [Fact]
    public void Vat_is_rounded_to_the_cent_and_the_total_is_the_sum_of_the_parts()
    {
        QuoteLineInput[] lines = [new(1, 0m, 33.33m)];

        var totals = QuoteCalculator.Calculate(lines, Vat);

        totals.VatAmount.ShouldBe(5.00m);
        totals.TotalIncVat.ShouldBe(totals.SubtotalExVat + totals.VatAmount);
        totals.TotalIncVat.ShouldBe(38.33m);
    }

    [Fact]
    public void The_vat_rate_comes_from_the_caller_not_the_calculator()
    {
        QuoteLineInput[] lines = [new(1, 0m, 100m)];

        QuoteCalculator.Calculate(lines, 0.15m).VatAmount.ShouldBe(15m);
        QuoteCalculator.Calculate(lines, 0.16m).VatAmount.ShouldBe(16m);
        QuoteCalculator.Calculate(lines, 0m).VatAmount.ShouldBe(0m);
    }

    [Fact]
    public void Fractional_quantities_are_priced_exactly()
    {
        QuoteLineInput[] lines = [new(2.5m, 10m, 24m)];

        var totals = QuoteCalculator.Calculate(lines, Vat);

        totals.LineTotals.ShouldBe([60m]);
        totals.InternalCostTotal.ShouldBe(25m);
    }

    [Fact]
    public void An_empty_quote_is_zero_with_no_margin()
    {
        var totals = QuoteCalculator.Calculate([], Vat);

        totals.SubtotalExVat.ShouldBe(0m);
        totals.TotalIncVat.ShouldBe(0m);
        totals.MarginPercent.ShouldBeNull();
    }

    [Fact]
    public void Selling_below_cost_gives_a_negative_margin_not_an_error()
    {
        QuoteLineInput[] lines = [new(1, 200m, 150m)];

        QuoteCalculator.Calculate(lines, Vat).MarginPercent.ShouldBe(-33.33m);
    }

    [Fact]
    public void A_free_line_still_counts_its_cost()
    {
        QuoteLineInput[] lines = [new(1, 100m, 0m), new(1, 0m, 100m)];

        var totals = QuoteCalculator.Calculate(lines, Vat);

        totals.InternalCostTotal.ShouldBe(100m);
        totals.MarginPercent.ShouldBe(0m);
    }
}

public class QuoteRulesTests
{
    [Theory]
    [InlineData(100_000.01, 100_000, false, true)]
    [InlineData(100_000.00, 100_000, false, false)]
    [InlineData(99_999.99, 100_000, false, false)]
    public void Only_a_quote_above_the_threshold_needs_approval(double total, double threshold, bool approved, bool expected)
    {
        QuoteRules.RequiresApproval((decimal)total, (decimal)threshold, approved ? Guid.NewGuid() : null).ShouldBe(expected);
    }

    [Fact]
    public void An_approved_quote_above_the_threshold_no_longer_needs_approval()
    {
        QuoteRules.RequiresApproval(500_000m, 100_000m, Guid.NewGuid()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(QuoteStatus.Draft, EditOutcome.InPlace)]
    [InlineData(QuoteStatus.PendingApproval, EditOutcome.InPlaceAndReopen)]
    [InlineData(QuoteStatus.Approved, EditOutcome.InPlaceAndReopen)]
    [InlineData(QuoteStatus.Issued, EditOutcome.NewVersion)]
    [InlineData(QuoteStatus.Accepted, EditOutcome.Refused)]
    [InlineData(QuoteStatus.Superseded, EditOutcome.Refused)]
    public void Editing_depends_on_how_far_the_quote_has_got(QuoteStatus status, EditOutcome expected)
    {
        QuoteRules.OnEdit(status).ShouldBe(expected);
    }

    [Fact]
    public void Each_step_is_only_open_from_the_right_status()
    {
        foreach (var status in Enum.GetValues<QuoteStatus>())
        {
            QuoteRules.CanSubmit(status).ShouldBe(status == QuoteStatus.Draft);
            QuoteRules.CanApprove(status).ShouldBe(status == QuoteStatus.PendingApproval);
            QuoteRules.CanAccept(status).ShouldBe(status == QuoteStatus.Issued);
            QuoteRules.CanIssue(status).ShouldBe(status is QuoteStatus.Draft or QuoteStatus.PendingApproval or QuoteStatus.Approved);
        }
    }

    [Fact]
    public void A_finished_quote_can_never_be_issued_again_or_edited_in_place()
    {
        QuoteRules.CanIssue(QuoteStatus.Issued).ShouldBeFalse();
        QuoteRules.CanIssue(QuoteStatus.Accepted).ShouldBeFalse();
        QuoteRules.CanIssue(QuoteStatus.Superseded).ShouldBeFalse();
    }
}

public class MarginBandTests
{
    private static readonly List<MarginBand> Bands =
    [
        new() { MinPax = 0, MaxPax = 100, TargetMinPct = 25, TargetMaxPct = 35 },
        new() { MinPax = 101, MaxPax = 300, TargetMinPct = 20, TargetMaxPct = 30 },
    ];

    [Theory]
    [InlineData(0, 25)]
    [InlineData(100, 25)]
    [InlineData(101, 20)]
    [InlineData(300, 20)]
    public void The_band_covering_the_pack_size_is_chosen_including_its_edges(int pax, int expectedMin)
    {
        MarginBands.For(pax, Bands)!.TargetMinPct.ShouldBe(expectedMin);
    }

    [Fact]
    public void A_pack_size_outside_every_band_has_no_band()
    {
        MarginBands.For(5000, Bands).ShouldBeNull();
        MarginBands.For(50, []).ShouldBeNull();
    }
}
