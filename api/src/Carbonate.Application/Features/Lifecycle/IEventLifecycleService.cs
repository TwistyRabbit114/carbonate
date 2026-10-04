using Carbonate.Domain.Common;
using Carbonate.Domain.Lifecycle;

namespace Carbonate.Application.Features.Lifecycle;

/// <summary>
/// Applies FR-02 transitions: checks the state machine, saves, then tells the observers.
/// </summary>
public interface IEventLifecycleService
{
    /// <summary>
    /// Moves an event, on a person's instruction.
    /// </summary>
    /// <param name="rowVersion">
    /// Base64 from the event DTO. A mismatch is 409: someone else moved it while this screen was open.
    /// </param>
    /// <exception cref="Common.ProblemException">
    /// 404 not found or not visible · 409 invalid transition or stale rowVersion · 422 a precondition
    /// is not met.
    /// </exception>
    Task<EventStatus> TransitionAsync(
        Guid eventId,
        EventStatus to,
        string rowVersion,
        Guid actingUserId,
        CancellationToken ct = default);

    /// <summary>
    /// The transitions this event could take right now, for the SPA's drag targets.
    /// </summary>
    Task<IReadOnlyList<EventStatus>> AllowedTransitionsAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// <c>Enquired → ConfirmedInPlanning</c>, called by the commercial module once it has written the
    /// confirmation (US-12). Separate from <see cref="TransitionAsync"/> because the caller is inside
    /// its own transaction and has already proved the confirmation exists.
    /// </summary>
    Task ConfirmAsync(Guid eventId, Guid actingUserId, CancellationToken ct = default);

    /// <summary>
    /// Moves every event whose scheduled time has passed. Used by <c>EventTransitionWorker</c>.
    /// </summary>
    /// <returns>How many events moved.</returns>
    Task<int> ApplyDueTransitionsAsync(CancellationToken ct = default);

    /// <summary>
    /// The next moment any event needs attention, or null if none do. The worker sleeps until then.
    /// </summary>
    Task<DateTime?> NextDueAtAsync(CancellationToken ct = default);
}
