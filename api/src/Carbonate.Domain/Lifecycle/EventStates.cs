using Carbonate.Domain.Common;

namespace Carbonate.Domain.Lifecycle;

/// <summary>
/// An enquiry that has not been confirmed. It is not on the events board (FR-01) and nothing is
/// planned against it yet.
/// </summary>
public sealed class EnquiredState : IEventState
{
    public EventStatus Status => EventStatus.Enquired;

    public IReadOnlyCollection<EventStatus> Targets { get; } = [EventStatus.ConfirmedInPlanning];

    public TransitionResult CanMoveTo(EventStatus target, TransitionContext context)
    {
        if (target != EventStatus.ConfirmedInPlanning)
        {
            // Note an enquiry cannot be cancelled: FR-02 reaches Cancelled only from
            // ConfirmedInPlanning. An enquiry that comes to nothing is deleted, not cancelled.
            return TransitionResult.NotAllowed(Status, target);
        }

        return context.HasConfirmation
            ? TransitionResult.Ok
            : TransitionResult.Blocked(
                TransitionCodes.ConfirmationRequired,
                "Record a purchase order or a deposit before confirming the event.");
    }
}

/// <summary>Confirmed and being planned. The only state with a choice of exits.</summary>
public sealed class ConfirmedInPlanningState : IEventState
{
    public EventStatus Status => EventStatus.ConfirmedInPlanning;

    public IReadOnlyCollection<EventStatus> Targets { get; } = [EventStatus.InProgress, EventStatus.Cancelled];

    public TransitionResult CanMoveTo(EventStatus target, TransitionContext context) => target switch
    {
        // Automatic at StartsAt, or manual when the client pulls the start forward on the day.
        EventStatus.InProgress when context.Trigger == TransitionTrigger.Scheduled && context.Now < context.StartsAt =>
            TransitionResult.Blocked(TransitionCodes.NotYetDue, "The event has not reached its start time."),
        EventStatus.InProgress => TransitionResult.Ok,

        // Cancelling is a decision, never something a clock does.
        EventStatus.Cancelled when context.Trigger != TransitionTrigger.Manual =>
            TransitionResult.Blocked(TransitionCodes.ManualOnly, "An event can only be cancelled by a person."),
        EventStatus.Cancelled => TransitionResult.Ok,

        _ => TransitionResult.NotAllowed(Status, target),
    };
}

/// <summary>The event is running: load-in through strike.</summary>
public sealed class InProgressState : IEventState
{
    public EventStatus Status => EventStatus.InProgress;

    public IReadOnlyCollection<EventStatus> Targets { get; } = [EventStatus.Finished];

    public TransitionResult CanMoveTo(EventStatus target, TransitionContext context) => target switch
    {
        // Once it has started it finishes. Cancelling a running event is not a thing — that is a
        // debrief and a reconciliation, not a cancellation.
        EventStatus.Finished when context.Trigger == TransitionTrigger.Scheduled && context.Now < context.EndsAt =>
            TransitionResult.Blocked(TransitionCodes.NotYetDue, "The event has not reached its end time."),
        EventStatus.Finished => TransitionResult.Ok,

        _ => TransitionResult.NotAllowed(Status, target),
    };
}

/// <summary>Terminal. Reconciliation happens against a finished event but does not change its state.</summary>
public sealed class FinishedState : IEventState
{
    public EventStatus Status => EventStatus.Finished;

    public IReadOnlyCollection<EventStatus> Targets { get; } = [];

    public TransitionResult CanMoveTo(EventStatus target, TransitionContext context) =>
        TransitionResult.NotAllowed(Status, target);
}

/// <summary>Terminal. Reachable only from <see cref="ConfirmedInPlanningState"/> (FR-02).</summary>
public sealed class CancelledState : IEventState
{
    public EventStatus Status => EventStatus.Cancelled;

    public IReadOnlyCollection<EventStatus> Targets { get; } = [];

    public TransitionResult CanMoveTo(EventStatus target, TransitionContext context) =>
        TransitionResult.NotAllowed(Status, target);
}
