using Carbonate.Infrastructure.Features.Lifecycle;
using Shouldly;

namespace Carbonate.UnitTests.Lifecycle;

/// <summary>
/// The worker's sleep calculation. Worth its own tests because getting it wrong is expensive rather
/// than merely wrong: a short loop keeps the serverless database awake and breaks the cost model
/// (docs/hosting.md, decision D-001).
/// </summary>
public class EventTransitionWorkerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sleeps_until_the_next_due_event() =>
        EventTransitionWorker.SleepUntil(Now.AddMinutes(7), Now).ShouldBe(TimeSpan.FromMinutes(7));

    [Fact]
    public void Caps_a_long_wait_at_six_hours()
    {
        // An event three weeks out must not mean three weeks of blindness: a new event created in the
        // meantime has to be picked up without a restart. Six hours rather than thirty minutes so the
        // database can reach its 60-minute auto-pause threshold between wakes (D-012) — at thirty
        // minutes it never paused and database compute was 92% of the bill.
        EventTransitionWorker.SleepUntil(Now.AddDays(21), Now).ShouldBe(TimeSpan.FromHours(6));
    }

    [Fact]
    public void The_cap_clears_the_database_auto_pause_threshold()
    {
        // The whole point of the cap's value. Azure SQL serverless pauses after 60 minutes of no
        // activity; a cap at or below that guarantees it never does.
        EventTransitionWorker.SleepUntil(null, Now)
            .ShouldBeGreaterThan(TimeSpan.FromHours(1));
    }

    [Fact]
    public void A_scheduled_event_still_transitions_on_time_despite_the_cap()
    {
        // Raising the cap does not make scheduled transitions late: the wait is computed from the
        // next due event, so anything inside the cap window is slept exactly.
        EventTransitionWorker.SleepUntil(Now.AddHours(2), Now).ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public void Waits_the_cap_when_nothing_is_scheduled() =>
        EventTransitionWorker.SleepUntil(null, Now).ShouldBe(TimeSpan.FromHours(6));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-6000)]
    public void Never_returns_zero_or_less(int minutesFromNow)
    {
        // Something already due means the cycle that just ran should have moved it. Waiting the floor
        // rather than returning zero stops a bug becoming a spin loop against the database.
        var sleep = EventTransitionWorker.SleepUntil(Now.AddMinutes(minutesFromNow), Now);

        sleep.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Never_sleeps_longer_than_the_cap()
    {
        DateTime?[] cases =
            [null, Now.AddSeconds(1), Now.AddMinutes(29), Now.AddMinutes(31), Now.AddYears(5), Now.AddDays(-1)];

        foreach (var next in cases)
        {
            EventTransitionWorker.SleepUntil(next, Now).ShouldBeLessThanOrEqualTo(TimeSpan.FromHours(6));
        }
    }
}
