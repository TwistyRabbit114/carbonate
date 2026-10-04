using Carbonate.Application.Features.Calendar;
using Carbonate.Infrastructure.Features.Calendar;
using Shouldly;

namespace Carbonate.UnitTests.Features.Calendar;

/// <summary>FR-40: the retry schedule, and the worker's sleep.</summary>
public class CalendarBackoffTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void Doubles_with_each_attempt(int attempts, int expectedMinutes) =>
        CalendarBackoff.For(attempts).ShouldBe(TimeSpan.FromMinutes(expectedMinutes));

    [Theory]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(1000)]
    public void Holds_at_an_hour_however_many_times_it_has_failed(int attempts)
    {
        // A genuine Google outage should not have Carbonate retrying every minute for a day, and a
        // large attempt count must not overflow into an absurd delay.
        CalendarBackoff.For(attempts).ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void A_first_push_is_not_delayed_long() =>
        CalendarBackoff.For(0).ShouldBe(TimeSpan.FromSeconds(30));

    [Fact]
    public void Sleeps_until_the_next_row_is_due() =>
        CalendarSyncWorker.SleepUntil(Now.AddMinutes(3), Now).ShouldBe(TimeSpan.FromMinutes(3));

    [Fact]
    public void Waits_the_cap_when_the_outbox_is_empty() =>
        CalendarSyncWorker.SleepUntil(null, Now).ShouldBe(TimeSpan.FromMinutes(15));

    [Fact]
    public void Caps_a_long_wait() =>
        CalendarSyncWorker.SleepUntil(Now.AddHours(5), Now).ShouldBe(TimeSpan.FromMinutes(15));

    [Fact]
    public void Never_spins_on_an_overdue_row() =>
        CalendarSyncWorker.SleepUntil(Now.AddMinutes(-10), Now).ShouldBe(TimeSpan.FromSeconds(10));
}
