namespace Carbonate.Application.Features.Calendar;

/// <summary>
/// How long to wait before retrying a failed calendar push.
/// </summary>
/// <remarks>
/// Pure so it can be tested without waiting for real time to pass. Doubling, capped at an hour: a
/// transient blip clears in seconds, and a genuine outage should not have Carbonate hammering Google
/// every minute for a day.
/// </remarks>
public static class CalendarBackoff
{
    private static readonly TimeSpan Cap = TimeSpan.FromHours(1);

    /// <param name="attempts">Attempts made so far, including the one that just failed.</param>
    public static TimeSpan For(int attempts)
    {
        if (attempts <= 0)
        {
            return TimeSpan.FromSeconds(30);
        }

        // 1 min, 2, 4, 8 … then held at the cap. Shifted rather than Math.Pow so a large attempt
        // count cannot overflow into something absurd.
        var minutes = attempts >= 6 ? 64 : 1 << (attempts - 1);
        var delay = TimeSpan.FromMinutes(minutes);

        return delay > Cap ? Cap : delay;
    }
}
