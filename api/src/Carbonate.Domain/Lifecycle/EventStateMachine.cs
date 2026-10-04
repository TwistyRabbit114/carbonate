using Carbonate.Domain.Common;

namespace Carbonate.Domain.Lifecycle;

/// <summary>
/// The whole of FR-02's transition rules, with no database and no side effects. Ask it a question,
/// get an answer; the service layer decides what to do about it.
/// </summary>
public static class EventStateMachine
{
    private static readonly Dictionary<EventStatus, IEventState> States =
        new IEventState[]
        {
            new EnquiredState(),
            new ConfirmedInPlanningState(),
            new InProgressState(),
            new FinishedState(),
            new CancelledState(),
        }.ToDictionary(s => s.Status);

    public static IEventState For(EventStatus status) =>
        States.TryGetValue(status, out var state)
            ? state
            : throw new ArgumentOutOfRangeException(nameof(status), status, "No state class for that status.");

    /// <summary>Whether the move is allowed, and why not if it is not.</summary>
    public static TransitionResult Check(EventStatus from, EventStatus to, TransitionContext context) =>
        from == to
            // Re-sending the current state is a no-op in the caller's mind but a bug in ours: it
            // would publish a state-changed event for a state that did not change.
            ? TransitionResult.NotAllowed(from, to)
            : For(from).CanMoveTo(to, context);

    /// <summary>
    /// The transitions a user could take right now, for <c>GET /api/events/{id}/allowed-transitions</c>.
    /// Only the ones that would actually succeed, so the SPA can enable exactly the right drag targets.
    /// </summary>
    public static IReadOnlyList<EventStatus> AllowedFrom(EventStatus from, TransitionContext context) =>
        [.. For(from).Targets.Where(target => For(from).CanMoveTo(target, context).Allowed)];

    /// <summary>
    /// The status an event should be in given the time, or null if it is where it belongs. This is what
    /// <c>EventTransitionWorker</c> acts on.
    /// </summary>
    public static EventStatus? DueStatus(EventStatus current, DateTime now, DateTime startsAt, DateTime endsAt)
    {
        // An event whose whole window has passed while nobody was looking — the app was down, or the
        // worker was restarting — goes to InProgress first, so no observer misses a step.
        if (current == EventStatus.ConfirmedInPlanning && now >= startsAt)
        {
            return EventStatus.InProgress;
        }

        return current == EventStatus.InProgress && now >= endsAt ? EventStatus.Finished : null;
    }

    /// <summary>
    /// When this event next needs the worker's attention, or null if it never will again.
    /// </summary>
    /// <remarks>
    /// The worker sleeps until the earliest of these rather than polling, because a polling loop keeps
    /// the serverless database awake and breaks the cost model (decisions D-001, docs/hosting.md).
    /// </remarks>
    public static DateTime? NextDueAt(EventStatus current, DateTime startsAt, DateTime endsAt) => current switch
    {
        EventStatus.ConfirmedInPlanning => startsAt,
        EventStatus.InProgress => endsAt,
        _ => null,
    };
}
