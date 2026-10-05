using Carbonate.Application.Features.Calendar;
using Carbonate.Domain.Common;
using Shouldly;

namespace Carbonate.UnitTests.Features.Calendar;

/// <summary>
/// FR-42. Google Calendar sits outside Carbonate's security boundary, so these tests are really
/// about what must <b>not</b> appear in a payload.
/// </summary>
public class CalendarPayloadTests
{
    private static readonly DateTime StartsAt = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndsAt = new(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc);

    private static CalendarEventPayload Event(bool confidential = false) =>
        CalendarPayloadBuilder.ForEvent(
            "NAI-WED-26", "Naidoo Wedding", EventType.Wedding, StartsAt, EndsAt, "Steenberg Estate", confidential);

    [Fact]
    public void A_normal_event_is_titled_with_its_code_and_name() =>
        Event().Title.ShouldBe("NAI-WED-26 — Naidoo Wedding");

    [Fact]
    public void A_confidential_event_does_not_name_itself()
    {
        var payload = Event(confidential: true);

        payload.Title.ShouldBe("Confidential event — NAI-WED-26");
        payload.Title.ShouldNotContain("Naidoo");
        payload.Description.ShouldNotContain("Naidoo");
    }

    [Fact]
    public void A_confidential_event_still_carries_its_time_so_the_team_sees_the_clash()
    {
        var payload = Event(confidential: true);

        payload.StartsAt.ShouldBe(StartsAt);
        payload.EndsAt.ShouldBe(EndsAt);
    }

    [Fact]
    public void The_venue_name_is_the_location_and_nothing_more() =>
        Event().Location.ShouldBe("Steenberg Estate");

    [Fact]
    public void A_venueless_event_has_no_location() =>
        CalendarPayloadBuilder
            .ForEvent("ENQ-TBC-26", "Unnamed enquiry", EventType.Corporate, StartsAt, EndsAt, null, false)
            .Location.ShouldBeNull();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_payload_ever_carries_a_headcount_or_a_money_value(bool confidential)
    {
        var payload = Event(confidential);

        // The event code is the only place a digit belongs. Anything else in the description would be
        // a pax count or an amount, and neither may leave Carbonate (FR-42).
        payload.Description.Any(char.IsDigit).ShouldBeFalse();
        payload.Description.ShouldNotContain("ZAR");
        payload.Description.ShouldNotContain("guests");
    }

    [Fact]
    public void Milestones_use_the_client_vocabulary()
    {
        // "Recce", "load-in", "strike" — Appendix C. Never "task" or "ticket".
        CalendarPayloadBuilder
            .ForMilestone("RIV-FEST-26", "Riverlight Festival", MilestoneType.SiteVisit,
                StartsAt, StartsAt.AddHours(2), "Riverside Grounds", false)
            .Title.ShouldBe("RIV-FEST-26 — Recce");

        CalendarPayloadBuilder
            .ForMilestone("RIV-FEST-26", "Riverlight Festival", MilestoneType.LoadIn,
                StartsAt, StartsAt.AddHours(4), "Riverside Grounds", false)
            .Title.ShouldBe("RIV-FEST-26 — Load-in");
    }

    [Fact]
    public void A_confidential_milestone_does_not_name_its_event()
    {
        var payload = CalendarPayloadBuilder.ForMilestone(
            "NAI-WED-26", "Naidoo Wedding", MilestoneType.Doors,
            StartsAt, StartsAt.AddHours(1), "Steenberg Estate", true);

        payload.Title.ShouldBe("NAI-WED-26 — Doors");
        payload.Description.ShouldNotContain("Naidoo");
    }

    [Fact]
    public void A_zero_length_milestone_is_given_a_duration()
    {
        // Doors is a moment, not a span. An entry that ends when it starts is rejected by Google and
        // invisible in a day view anyway.
        var payload = CalendarPayloadBuilder.ForMilestone(
            "MER-YE-26", "Meridian Year-End", MilestoneType.Doors, StartsAt, StartsAt, null, false);

        payload.EndsAt.ShouldBe(StartsAt.AddMinutes(30));
    }
}
