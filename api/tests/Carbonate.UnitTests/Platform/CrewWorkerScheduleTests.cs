using Carbonate.Infrastructure.Platform.Users;
using Shouldly;

namespace Carbonate.UnitTests.Platform;

public class CrewWorkerScheduleTests
{
    private static DateTime Utc(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Before_the_run_time_it_waits_until_today()
    {
        CrewAccountExpiryWorker.NextRunDelay(Utc(20, 0, 10)).ShouldBe(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void After_the_run_time_it_waits_until_tomorrow()
    {
        CrewAccountExpiryWorker.NextRunDelay(Utc(20, 1, 0)).ShouldBe(TimeSpan.FromHours(23.5));
    }

    [Fact]
    public void Exactly_at_the_run_time_it_waits_a_full_day_and_never_runs_twice()
    {
        CrewAccountExpiryWorker.NextRunDelay(Utc(20, 0, 30)).ShouldBe(TimeSpan.FromDays(1));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 29)]
    [InlineData(0, 31)]
    [InlineData(12, 0)]
    [InlineData(23, 59)]
    public void The_wait_is_always_positive_and_never_longer_than_a_day(int hour, int minute)
    {
        var delay = CrewAccountExpiryWorker.NextRunDelay(Utc(20, hour, minute));

        delay.ShouldBeGreaterThan(TimeSpan.Zero);
        delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromDays(1));
    }
}
