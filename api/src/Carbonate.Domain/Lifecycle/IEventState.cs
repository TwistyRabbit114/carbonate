using Carbonate.Domain.Common;

namespace Carbonate.Domain.Lifecycle;

/// <summary>
/// One state an event can be in (FR-02, State pattern, Task 1 §6.1). Each state owns the list of
/// places it can go and the conditions for going there.
/// </summary>
/// <remarks>
/// Deliberately knows nothing about the database, calendars or notifications. It is given everything
/// it needs in a <see cref="TransitionContext"/> and answers a question; the service layer does the
/// work and the observers react. That is what makes the rules testable without a database.
/// </remarks>
public interface IEventState
{
    EventStatus Status { get; }

    /// <summary>True once the event can go nowhere else. <c>Finished</c> and <c>Cancelled</c>.</summary>
    bool IsTerminal => Targets.Count == 0;

    /// <summary>Every state reachable from here, regardless of whether conditions are currently met.</summary>
    IReadOnlyCollection<EventStatus> Targets { get; }

    /// <summary>Whether this particular move is allowed right now, and why not if it is not.</summary>
    TransitionResult CanMoveTo(EventStatus target, TransitionContext context);
}

/// <summary>What triggered the move, because some transitions are manual-only and some are automatic.</summary>
public enum TransitionTrigger
{
    /// <summary>A person pressed something.</summary>
    Manual,

    /// <summary><see cref="EventStatus"/> changed by the background worker reaching a scheduled time.</summary>
    Scheduled,

    /// <summary>A confirmation was recorded, which is the only way out of <c>Enquired</c> (US-12).</summary>
    Confirmation,
}

/// <summary>
/// Everything a state needs to judge a transition. A record so a test can build one in a line.
/// </summary>
/// <param name="Trigger">Who or what is asking.</param>
/// <param name="Now">Supplied rather than read, so tests are not at the mercy of the clock.</param>
/// <param name="StartsAt">When the event is due to start, in UTC.</param>
/// <param name="EndsAt">When the event is due to end, in UTC.</param>
/// <param name="HasConfirmation">Whether an EVENT_CONFIRMATION exists (US-12).</param>
public readonly record struct TransitionContext(
    TransitionTrigger Trigger,
    DateTime Now,
    DateTime StartsAt,
    DateTime EndsAt,
    bool HasConfirmation = false);

/// <param name="Allowed">Whether the move may go ahead.</param>
/// <param name="Code">
/// Machine-readable reason for a refusal, so the API can pick the right status code and the SPA can
/// show a useful message. Null when allowed.
/// </param>
/// <param name="Reason">A sentence safe to show a user.</param>
public readonly record struct TransitionResult(bool Allowed, string? Code = null, string? Reason = null)
{
    public static TransitionResult Ok { get; } = new(true);

    /// <summary>The move is not on the map at all. The API reports 409.</summary>
    public static TransitionResult NotAllowed(EventStatus from, EventStatus to) =>
        new(false, TransitionCodes.InvalidTransition, $"An event cannot move from {from} to {to}.");

    /// <summary>The move exists but a precondition is unmet. The API reports 422.</summary>
    public static TransitionResult Blocked(string code, string reason) => new(false, code, reason);
}

public static class TransitionCodes
{
    public const string InvalidTransition = "INVALID_TRANSITION";
    public const string ConfirmationRequired = "CONFIRMATION_REQUIRED";
    public const string NotYetDue = "NOT_YET_DUE";
    public const string ManualOnly = "MANUAL_ONLY";
}
