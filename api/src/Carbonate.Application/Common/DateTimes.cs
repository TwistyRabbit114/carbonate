namespace Carbonate.Application.Common;

public static class DateTimes
{
    /// <summary>
    /// Everything is stored and compared as UTC. A value with an offset arrives as local time and one
    /// with no offset has no kind; comparing those with stored UTC values would be wrong by hours.
    /// </summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
