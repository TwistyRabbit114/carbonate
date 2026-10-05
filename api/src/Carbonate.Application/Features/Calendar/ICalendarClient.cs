namespace Carbonate.Application.Features.Calendar;

/// <summary>
/// The only way Carbonate talks to Google Calendar.
/// </summary>
/// <remarks>
/// <b>Outbound only.</b> Insert, update and delete — never a read. Carbonate is the record of truth
/// for what is happening; the calendar is a convenience copy, and reading it back would invite two
/// systems disagreeing about which is right.
/// </remarks>
public interface ICalendarClient
{
    /// <summary>Whether a calendar is connected at all. False means the outbox simply waits.</summary>
    Task<bool> IsConnectedAsync(CancellationToken ct = default);

    /// <summary>Creates an entry and returns Google's id for it, which goes into CALENDAR_LINK.</summary>
    Task<string> InsertAsync(CalendarEventPayload payload, CancellationToken ct = default);

    /// <summary>Updates the entry a previous insert created (FR-41: never a second copy).</summary>
    Task UpdateAsync(string googleEventId, CalendarEventPayload payload, CancellationToken ct = default);

    /// <summary>Removes the entry. Missing is success — the end state is what matters.</summary>
    Task DeleteAsync(string googleEventId, CancellationToken ct = default);
}

/// <summary>
/// Thrown when Google says the refresh token is no longer valid, so the UI can show "reconnect
/// needed" rather than a generic failure.
/// </summary>
/// <remarks>
/// Expected rather than exceptional: while the OAuth consent screen is in Testing with an External
/// user type, Google expires refresh tokens after seven days. The calendar has to be reconnected
/// before the demo.
/// </remarks>
public sealed class CalendarReauthRequiredException(string message) : Exception(message);
