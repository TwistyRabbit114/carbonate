using Carbonate.Domain.Platform;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

public class CrewExpiryTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 2, 30, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(7);

    private static CrewAssignmentFact Worked(double debriefDaysAgo, double shiftDaysAgo = 9) =>
        new(Now.AddDays(-shiftDaysAgo), Now.AddDays(-debriefDaysAgo));

    [Fact]
    public void An_account_is_deactivated_once_every_debrief_ended_more_than_seven_days_ago()
    {
        CrewExpiry.ShouldDeactivate(true, [Worked(8), Worked(30)], Now, Grace).ShouldBeTrue();
    }

    [Fact]
    public void An_account_stays_active_until_the_debrief_is_a_full_seven_days_past()
    {
        CrewExpiry.ShouldDeactivate(true, [Worked(7)], Now, Grace).ShouldBeFalse();
        CrewExpiry.ShouldDeactivate(true, [Worked(6.9)], Now, Grace).ShouldBeFalse();
        CrewExpiry.ShouldDeactivate(true, [Worked(7.01)], Now, Grace).ShouldBeTrue();
    }

    [Fact]
    public void One_recent_event_keeps_the_account_even_if_the_others_are_long_over()
    {
        CrewExpiry.ShouldDeactivate(true, [Worked(60), Worked(2)], Now, Grace).ShouldBeFalse();
    }

    [Fact]
    public void A_future_shift_keeps_the_account_even_when_every_past_event_is_over()
    {
        var future = new CrewAssignmentFact(Now.AddDays(3), Now.AddDays(10));

        CrewExpiry.ShouldDeactivate(true, [Worked(30), future], Now, Grace).ShouldBeFalse();
    }

    [Fact]
    public void An_event_with_no_debrief_milestone_keeps_the_account_open()
    {
        var noDebrief = new CrewAssignmentFact(Now.AddDays(-20), null);

        CrewExpiry.ShouldDeactivate(true, [Worked(30), noDebrief], Now, Grace).ShouldBeFalse();
    }

    [Fact]
    public void An_account_with_no_assignments_is_left_alone()
    {
        CrewExpiry.ShouldDeactivate(true, [], Now, Grace).ShouldBeFalse();
    }

    [Fact]
    public void Only_accounts_that_hold_nothing_but_the_casual_crew_role_expire()
    {
        CrewExpiry.ShouldDeactivate(false, [Worked(30)], Now, Grace).ShouldBeFalse();
    }

    [Fact]
    public void The_grace_period_is_configurable()
    {
        CrewExpiry.ShouldDeactivate(true, [Worked(4)], Now, TimeSpan.FromDays(3)).ShouldBeTrue();
        CrewExpiry.ShouldDeactivate(true, [Worked(4)], Now, TimeSpan.FromDays(7)).ShouldBeFalse();
    }

    [Fact]
    public void A_shift_that_ended_exactly_now_is_not_in_the_future()
    {
        var endsNow = new CrewAssignmentFact(Now, Now.AddDays(-30));

        CrewExpiry.ShouldDeactivate(true, [endsNow], Now, Grace).ShouldBeTrue();
    }
}
