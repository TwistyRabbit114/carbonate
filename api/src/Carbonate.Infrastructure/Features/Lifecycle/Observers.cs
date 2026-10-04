using Carbonate.Application.Platform.Audit;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Calendar;
using Carbonate.Domain.Lifecycle;
using Carbonate.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Features.Lifecycle;

/// <summary>
/// Writes the audit entry for every transition (FR-02 Observer, Task 1 §7.4).
/// </summary>
internal sealed class AuditObserver(IAuditService audit) : IEventStateObserver
{
    public Task OnStateChangedAsync(EventStateChanged change, CancellationToken ct = default) =>
        audit.RecordAsync(
            action: "EventTransition",
            entityName: "Event",
            entityId: change.EventId.ToString(),
            before: new { Status = change.From.ToString() },
            after: new { Status = change.To.ToString(), Trigger = change.Trigger.ToString() },
            // Null for the scheduler: the audit row records a system actor rather than pretending a
            // person did it. The actor string carries the detail.
            userId: Guid.TryParse(change.Actor, out var userId) ? userId : null,
            ct);
}

/// <summary>
/// Queues the event for a push to Google Calendar (FR-40–42).
/// </summary>
/// <remarks>
/// Writes an outbox row and nothing else. It makes no network call, so a slow or unavailable Google
/// can never slow down or fail a user's transition (NFR-12). <c>CalendarSyncWorker</c> drains the
/// outbox, and checks CALENDAR_LINK first so an update targets the existing Google event rather than
/// creating a duplicate (FR-41).
/// </remarks>
internal sealed class CalendarSyncObserver(CemDbContext db, TimeProvider clock) : IEventStateObserver
{
    public async Task OnStateChangedAsync(EventStateChanged change, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        db.CalendarOutbox.Add(new CalendarOutbox
        {
            SourceEntityType = CalendarSourceType.Event,
            SourceEntityId = change.EventId,
            // A cancelled event comes off the calendar; everything else is an insert or an update,
            // which the worker tells apart by looking for an existing link.
            Operation = change.To == EventStatus.Cancelled ? OutboxOperation.Delete : OutboxOperation.Upsert,
            EnqueuedAt = now,
            Attempts = 0,
            NextAttemptAt = now,
        });

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Tells the event manager the event moved (FR-02 Observer).
/// </summary>
/// <remarks>
/// TODO(plan): the schema has no notification table, and the plan says an in-app notification is
/// enough without saying where it lives. For now this logs, so the observer wiring is real and
/// testable and there is one obvious place to add persistence once B and C agree where notifications
/// are stored and shown. Raised in the FR-02 PR.
/// </remarks>
internal sealed class NotificationObserver(ILogger<NotificationObserver> logger) : IEventStateObserver
{
    public Task OnStateChangedAsync(EventStateChanged change, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Notify: {EventCode} moved from {From} to {To} ({Trigger}) by {Actor}.",
            change.EventCode, change.From, change.To, change.Trigger, change.Actor);

        return Task.CompletedTask;
    }
}
