using Carbonate.Domain.Common;

namespace Carbonate.Domain.Lifecycle;

/// <summary>
/// Published after every transition, manual or automatic (FR-02, Observer pattern, Task 1 §6.1).
/// </summary>
/// <param name="Actor">
/// Who caused it: a user id, or <see cref="SystemActor"/> when the background worker did.
/// </param>
public sealed record EventStateChanged(
    Guid EventId,
    string EventCode,
    EventStatus From,
    EventStatus To,
    TransitionTrigger Trigger,
    string Actor,
    DateTime OccurredAt)
{
    /// <summary>What the worker records as the actor, so the audit trail distinguishes it from a person.</summary>
    public const string SystemActor = "system:scheduler";
}

/// <summary>
/// Something that reacts to a transition. Implementations are the calendar sync, the audit entry and
/// the notification — the state machine references none of them.
/// </summary>
public interface IEventStateObserver
{
    /// <remarks>
    /// Must not throw: one observer failing cannot undo a transition that has already happened, nor
    /// stop the others running. Implementations log and move on.
    /// </remarks>
    Task OnStateChangedAsync(EventStateChanged change, CancellationToken ct = default);
}
