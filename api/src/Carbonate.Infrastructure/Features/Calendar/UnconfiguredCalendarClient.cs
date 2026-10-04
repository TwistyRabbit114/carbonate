using Carbonate.Application.Features.Calendar;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Features.Calendar;

/// <summary>
/// Stands in until the Google OAuth connect flow lands.
/// </summary>
/// <remarks>
/// <para>
/// Reports "not connected", which is the honest answer before anyone has connected a calendar. The
/// outbox worker sees that and leaves every row where it is, so nothing is lost and the queue drains
/// the moment a real client is registered.
/// </para>
/// <para>
/// TODO(plan): replace with the Google client once the OAuth consent screen and client credentials
/// exist. The refresh token goes to Key Vault under <c>CALENDAR_ACCOUNT.TokenSecretName</c>; the
/// database holds only that name.
/// </para>
/// </remarks>
internal sealed class UnconfiguredCalendarClient(ILogger<UnconfiguredCalendarClient> logger) : ICalendarClient
{
    public Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        logger.LogDebug("No Google Calendar client configured; the outbox is holding.");
        return Task.FromResult(false);
    }

    // Unreachable while IsConnectedAsync is false. Throwing rather than silently succeeding means a
    // wiring mistake shows up as a retried outbox row, not as a transition that quietly never synced.
    public Task<string> InsertAsync(CalendarEventPayload payload, CancellationToken ct = default) =>
        throw new InvalidOperationException("No Google Calendar is connected.");

    public Task UpdateAsync(string googleEventId, CalendarEventPayload payload, CancellationToken ct = default) =>
        throw new InvalidOperationException("No Google Calendar is connected.");

    public Task DeleteAsync(string googleEventId, CancellationToken ct = default) =>
        throw new InvalidOperationException("No Google Calendar is connected.");
}
