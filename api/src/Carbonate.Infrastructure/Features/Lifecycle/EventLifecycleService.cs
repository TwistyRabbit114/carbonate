using Carbonate.Application.Common;
using Carbonate.Application.Features.Lifecycle;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Lifecycle;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Features.Lifecycle;

/// <summary>FR-02. The state machine decides; this saves the result and notifies the observers.</summary>
internal sealed class EventLifecycleService(
    CemDbContext db,
    IEnumerable<IEventStateObserver> observers,
    TimeProvider clock,
    ILogger<EventLifecycleService> logger) : IEventLifecycleService
{
    public async Task<EventStatus> TransitionAsync(
        Guid eventId,
        EventStatus to,
        string rowVersion,
        Guid actingUserId,
        CancellationToken ct = default)
    {
        var ev = await LoadAsync(eventId, ct);
        var context = await BuildContextAsync(ev, TransitionTrigger.Manual, ct);

        Guard(EventStateMachine.Check(ev.Status, to, context));

        // Set the original value rather than comparing by hand, so EF raises the concurrency conflict
        // and we do not race between reading and writing.
        db.Entry(ev).Property(e => e.RowVersion).OriginalValue = DecodeRowVersion(rowVersion);

        var from = ev.Status;
        ev.Status = to;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ProblemException.Conflict(
                "/problems/concurrency",
                "Someone else changed this event.",
                "Reload the event and try again.");
        }

        await PublishAsync(ev, from, to, TransitionTrigger.Manual, actingUserId.ToString(), ct);
        return to;
    }

    public async Task<IReadOnlyList<EventStatus>> AllowedTransitionsAsync(
        Guid eventId,
        CancellationToken ct = default)
    {
        var ev = await LoadAsync(eventId, ct);
        var context = await BuildContextAsync(ev, TransitionTrigger.Manual, ct);

        return EventStateMachine.AllowedFrom(ev.Status, context);
    }

    public async Task ConfirmAsync(Guid eventId, Guid actingUserId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(eventId, ct);
        var context = await BuildContextAsync(ev, TransitionTrigger.Confirmation, ct);

        Guard(EventStateMachine.Check(ev.Status, EventStatus.ConfirmedInPlanning, context));

        var from = ev.Status;
        ev.Status = EventStatus.ConfirmedInPlanning;

        // No SaveChanges: the commercial module owns the transaction that wrote the confirmation, and
        // the event change belongs in it. Observers run after, on the caller's commit.
        await PublishAsync(ev, from, EventStatus.ConfirmedInPlanning, TransitionTrigger.Confirmation,
            actingUserId.ToString(), ct);
    }

    public async Task<int> ApplyDueTransitionsAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // One set-based query for the whole batch, not a query per event (NFR-08).
        var due = await db.Events
            .Where(e => e.IsActive
                        && ((e.Status == EventStatus.ConfirmedInPlanning && e.StartsAt <= now)
                            || (e.Status == EventStatus.InProgress && e.EndsAt <= now)))
            .OrderBy(e => e.StartsAt)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return 0;
        }

        var changes = new List<(Event Event, EventStatus From, EventStatus To)>();

        foreach (var ev in due)
        {
            var to = EventStateMachine.DueStatus(ev.Status, now, ev.StartsAt, ev.EndsAt);
            if (to is null)
            {
                continue;
            }

            var context = new TransitionContext(TransitionTrigger.Scheduled, now, ev.StartsAt, ev.EndsAt);
            var check = EventStateMachine.Check(ev.Status, to.Value, context);
            if (!check.Allowed)
            {
                // Should not happen — DueStatus and the state machine agree — but a refusal here is a
                // bug worth seeing rather than a transition worth forcing.
                logger.LogError(
                    "Event {EventCode} was due to move to {To} but the state machine refused: {Code}.",
                    ev.EventCode, to, check.Code);
                continue;
            }

            changes.Add((ev, ev.Status, to.Value));
            ev.Status = to.Value;
        }

        if (changes.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(ct);

        foreach (var (ev, from, to) in changes)
        {
            await PublishAsync(ev, from, to, TransitionTrigger.Scheduled, EventStateChanged.SystemActor, ct);
        }

        return changes.Count;
    }

    public async Task<DateTime?> NextDueAtAsync(CancellationToken ct = default)
    {
        // Two cheap indexed minimums rather than loading events and asking each one.
        var nextStart = await db.Events
            .Where(e => e.IsActive && e.Status == EventStatus.ConfirmedInPlanning)
            .MinAsync(e => (DateTime?)e.StartsAt, ct);

        var nextEnd = await db.Events
            .Where(e => e.IsActive && e.Status == EventStatus.InProgress)
            .MinAsync(e => (DateTime?)e.EndsAt, ct);

        return (nextStart, nextEnd) switch
        {
            (null, null) => null,
            (null, { } end) => end,
            ({ } start, null) => start,
            var (start, end) => start < end ? start : end,
        };
    }

    private async Task<Event> LoadAsync(Guid eventId, CancellationToken ct) =>
        await db.Events.FirstOrDefaultAsync(e => e.EventId == eventId && e.IsActive, ct)
        ?? throw ProblemException.NotFound();

    private async Task<TransitionContext> BuildContextAsync(Event ev, TransitionTrigger trigger, CancellationToken ct)
    {
        // Only Enquired cares, so only Enquired pays for the query.
        var hasConfirmation = ev.Status == EventStatus.Enquired
                              && await db.EventConfirmations.AnyAsync(c => c.EventId == ev.EventId, ct);

        return new TransitionContext(trigger, clock.GetUtcNow().UtcDateTime, ev.StartsAt, ev.EndsAt, hasConfirmation);
    }

    /// <summary>
    /// Turns a refusal into the right status code: a move that is not on the map is a 409, a move
    /// blocked by an unmet precondition is a 422.
    /// </summary>
    private static void Guard(TransitionResult result)
    {
        if (result.Allowed)
        {
            return;
        }

        var reason = result.Reason ?? "That change is not allowed.";

        throw result.Code == TransitionCodes.InvalidTransition
            ? ProblemException.Conflict("/problems/invalid-transition", "Not a valid change.", reason)
            : ProblemException.BusinessRule(reason);
    }

    /// <summary>
    /// Tells every observer, one failure at a time. The transition has already happened and is already
    /// saved; an observer throwing must not undo it or stop the others (FR-02, NFR-12).
    /// </summary>
    private async Task PublishAsync(
        Event ev,
        EventStatus from,
        EventStatus to,
        TransitionTrigger trigger,
        string actor,
        CancellationToken ct)
    {
        var change = new EventStateChanged(
            ev.EventId, ev.EventCode, from, to, trigger, actor, clock.GetUtcNow().UtcDateTime);

        foreach (var observer in observers)
        {
            try
            {
                await observer.OnStateChangedAsync(change, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "{Observer} failed for {EventCode} moving {From} to {To}. The transition stands.",
                    observer.GetType().Name, ev.EventCode, from, to);
            }
        }
    }

    private static byte[] DecodeRowVersion(string rowVersion)
    {
        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["Not a valid row version."],
            });
        }
    }
}
