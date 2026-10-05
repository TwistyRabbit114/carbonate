using Carbonate.Application.Features.Calendar;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Calendar;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Features.Calendar;

/// <summary>
/// Drains CALENDAR_OUTBOX to Google (FR-40–42, NFR-12).
/// </summary>
/// <remarks>
/// <para>
/// The outbox exists so a user's request never waits on Google. Observers write a row and return;
/// this worker does the slow, failure-prone part out of band.
/// </para>
/// <para>
/// It <b>never drops a row</b>. A failure increases the attempt count and pushes
/// <c>NextAttemptAt</c> out; only a success removes the row.
/// </para>
/// <para>
/// Like the transition worker it sleeps rather than polls, for the same reason: keeping the
/// serverless database awake is what the cost model cannot afford (docs/hosting.md).
/// </para>
/// </remarks>
internal sealed class CalendarSyncWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<CalendarSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxSleep = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MinSleep = TimeSpan.FromSeconds(10);

    /// <summary>Small, so one poisonous row cannot hold up a long batch.</summary>
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Calendar sync worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;

            try
            {
                delay = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Calendar sync cycle failed. Retrying shortly.");
                delay = TimeSpan.FromMinutes(1);
            }

            try
            {
                await Task.Delay(delay, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Calendar sync worker stopped.");
    }

    private async Task<TimeSpan> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CemDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<ICalendarClient>();

        if (!await client.IsConnectedAsync(ct))
        {
            // No calendar connected yet. The rows wait; nothing is lost, and the queue drains as soon
            // as someone connects one.
            return MaxSleep;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        var due = await db.CalendarOutbox
            .Where(o => o.NextAttemptAt <= now)
            .OrderBy(o => o.EnqueuedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in due)
        {
            await ProcessAsync(db, client, row, ct);
        }

        await db.SaveChangesAsync(ct);

        return await SleepAsync(db, ct);
    }

    private async Task ProcessAsync(CemDbContext db, ICalendarClient client, CalendarOutbox row, CancellationToken ct)
    {
        try
        {
            // The link is checked first, every time. This is what stops a retry, a restart or a second
            // observer creating a duplicate entry in Google (FR-41).
            var link = await db.CalendarLinks.FirstOrDefaultAsync(
                l => l.SourceEntityType == row.SourceEntityType && l.SourceEntityId == row.SourceEntityId, ct);

            if (row.Operation == OutboxOperation.Delete)
            {
                await DeleteAsync(db, client, link, ct);
            }
            else
            {
                await UpsertAsync(db, client, row, link, ct);
            }

            db.CalendarOutbox.Remove(row);
        }
        catch (CalendarReauthRequiredException ex)
        {
            // Not a transient failure: nothing will succeed until a person reconnects the calendar.
            // Backing off hard beats retrying a credential that cannot work.
            logger.LogWarning(ex, "Calendar needs reconnecting. Holding the outbox.");
            Fail(row, "Reconnect the Google Calendar.", clock.GetUtcNow().UtcDateTime, TimeSpan.FromHours(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            Fail(row, ex.Message, now, CalendarBackoff.For(row.Attempts + 1));

            logger.LogWarning(
                ex,
                "Calendar push failed for {Type} {Id}, attempt {Attempts}. Next try at {Next}.",
                row.SourceEntityType, row.SourceEntityId, row.Attempts, row.NextAttemptAt);
        }
    }

    private async Task UpsertAsync(
        CemDbContext db,
        ICalendarClient client,
        CalendarOutbox row,
        CalendarLink? link,
        CancellationToken ct)
    {
        var payload = await BuildPayloadAsync(db, row, ct);
        if (payload is null)
        {
            // The source is gone. Nothing to push and nothing to retry.
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        if (link is null)
        {
            var account = await db.CalendarAccounts
                .Where(a => a.RevokedAt == null)
                .OrderBy(a => a.ConnectedAt)
                .FirstAsync(ct);

            var googleEventId = await client.InsertAsync(payload.Value, ct);

            db.CalendarLinks.Add(new CalendarLink
            {
                AccountId = account.AccountId,
                SourceEntityType = row.SourceEntityType,
                SourceEntityId = row.SourceEntityId,
                GoogleEventId = googleEventId,
                LastPushedAt = now,
                SyncStatus = "Synced",
            });
        }
        else
        {
            await client.UpdateAsync(link.GoogleEventId, payload.Value, ct);
            link.LastPushedAt = now;
            link.SyncStatus = "Synced";
            link.LastError = null;
        }
    }

    private static async Task DeleteAsync(
        CemDbContext db,
        ICalendarClient client,
        CalendarLink? link,
        CancellationToken ct)
    {
        if (link is null)
        {
            // Never pushed, so there is nothing in Google to remove.
            return;
        }

        await client.DeleteAsync(link.GoogleEventId, ct);
        db.CalendarLinks.Remove(link);
    }

    /// <summary>Null when the source row has been deleted since the outbox row was written.</summary>
    private static async Task<CalendarEventPayload?> BuildPayloadAsync(
        CemDbContext db,
        CalendarOutbox row,
        CancellationToken ct)
    {
        if (row.SourceEntityType != CalendarSourceType.Event)
        {
            // TODO(plan): milestone and task-card payloads land with the boards module. The builder
            // already has ForMilestone; only the query is missing. Raised in the FR-40 PR.
            return null;
        }

        var source = await db.Events
            .Where(e => e.EventId == row.SourceEntityId)
            .Select(e => new
            {
                e.EventCode,
                e.Name,
                e.EventType,
                e.StartsAt,
                e.EndsAt,
                e.IsConfidential,
                VenueName = db.Venues.Where(v => v.VenueId == e.VenueId).Select(v => v.Name).FirstOrDefault(),
                // The client's own confidentiality flag counts too, not just the event's.
                ClientConfidential = db.Clients
                    .Where(c => c.ClientId == e.ClientId)
                    .Select(c => c.ConfidentialityRequired)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (source is null)
        {
            return null;
        }

        return CalendarPayloadBuilder.ForEvent(
            source.EventCode,
            source.Name,
            source.EventType,
            source.StartsAt,
            source.EndsAt,
            source.VenueName,
            source.IsConfidential || source.ClientConfidential);
    }

    private static void Fail(CalendarOutbox row, string error, DateTime now, TimeSpan backoff)
    {
        row.Attempts++;
        row.NextAttemptAt = now.Add(backoff);
        row.LastError = error.Length > 2000 ? error[..2000] : error;
    }

    /// <summary>Sleeps until the earliest row is due, so an empty outbox costs nothing.</summary>
    private async Task<TimeSpan> SleepAsync(CemDbContext db, CancellationToken ct)
    {
        var next = await db.CalendarOutbox.MinAsync(o => (DateTime?)o.NextAttemptAt, ct);

        return SleepUntil(next, clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>Internal so the clamping is testable without a host.</summary>
    internal static TimeSpan SleepUntil(DateTime? nextAttemptAt, DateTime now)
    {
        if (nextAttemptAt is null)
        {
            return MaxSleep;
        }

        var wait = nextAttemptAt.Value - now;

        if (wait <= TimeSpan.Zero)
        {
            return MinSleep;
        }

        return wait > MaxSleep ? MaxSleep : wait;
    }
}
