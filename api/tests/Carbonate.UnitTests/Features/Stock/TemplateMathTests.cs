using Carbonate.Application.Features.Stock;
using Carbonate.Domain.Common;
using Shouldly;

namespace Carbonate.UnitTests.Features.Stock;

/// <summary>FR-25: the two calculations the plan specifies for template seeding.</summary>
public class TemplateMathTests
{
    [Theory]
    // The plain case: 220 cups per hundred guests, 180 guests -> 396.
    [InlineData(220, 180, 396)]
    // Exactly a multiple of a hundred needs no rounding.
    [InlineData(45, 600, 270)]
    // 45 x 90 / 100 = 40.5, which has to round UP to 41, not to nearest.
    [InlineData(45, 90, 41)]
    // Anything above a whole number goes to the next one, however small the fraction.
    [InlineData(1, 101, 2)]
    // A festival pack size scales the same way as a wedding one.
    [InlineData(1, 2500, 25)]
    public void ScaleToPackSize_rounds_up(decimal perHundred, int packSize, decimal expected) =>
        TemplateMath.ScaleToPackSize(perHundred, packSize).ShouldBe(expected);

    [Fact]
    public void ScaleToPackSize_returns_zero_for_a_zero_pack_size() =>
        TemplateMath.ScaleToPackSize(220m, 0).ShouldBe(0m);

    [Fact]
    public void ScaleToPackSize_returns_zero_rather_than_a_negative_quantity() =>
        TemplateMath.ScaleToPackSize(-5m, 180).ShouldBe(0m);

    [Fact]
    public void ScaleToPackSize_rejects_a_negative_pack_size() =>
        Should.Throw<ArgumentOutOfRangeException>(() => TemplateMath.ScaleToPackSize(220m, -1));

    [Fact]
    public void DueAt_is_the_milestone_start_plus_the_offset()
    {
        var loadIn = new DateTime(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
        var milestones = new Dictionary<MilestoneType, DateTime> { [MilestoneType.LoadIn] = loadIn };

        // "Confirm the access route a week before load-in."
        TemplateMath.DueAt(MilestoneType.LoadIn, -168, milestones)
            .ShouldBe(new DateTime(2026, 11, 7, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DueAt_handles_a_positive_offset()
    {
        var debrief = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        var milestones = new Dictionary<MilestoneType, DateTime> { [MilestoneType.Debrief] = debrief };

        TemplateMath.DueAt(MilestoneType.Debrief, 24, milestones)
            .ShouldBe(new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DueAt_is_null_when_the_card_names_no_milestone() =>
        TemplateMath.DueAt(null, -48, new Dictionary<MilestoneType, DateTime>()).ShouldBeNull();

    [Fact]
    public void DueAt_is_null_when_the_event_has_no_milestone_of_that_type()
    {
        // A card due at an invented time is worse than a card with no due date.
        var milestones = new Dictionary<MilestoneType, DateTime>
        {
            [MilestoneType.LoadIn] = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc),
        };

        TemplateMath.DueAt(MilestoneType.Rehearsal, 0, milestones).ShouldBeNull();
    }
}
