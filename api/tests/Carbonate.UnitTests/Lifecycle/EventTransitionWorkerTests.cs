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
    public void Caps_a_long_wait_at_thirty_minutes()
    {
        // An event three weeks out must not mean three weeks of blindness: a new event created in the
        // meantime has to be picked up without a restart.
        EventTransitionWorker.SleepUntil(Now.AddDays(21), Now).ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Waits_the_cap_when_nothing_is_scheduled() =>
        EventTransitionWorker.SleepUntil(null, Now).ShouldBe(TimeSpan.FromMinutes(30));

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
            EventTransitionWorker.SleepUntil(next, Now).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(30));
        }
    }
}
