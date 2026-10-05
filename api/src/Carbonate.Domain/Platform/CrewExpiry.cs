namespace Carbonate.Domain.Platform;

/// <summary>One event a crew member is assigned to, as the expiry rule sees it.</summary>
/// <param name="ShiftEnd">When their shift on that event ends.</param>
/// <param name="DebriefEnd">
/// When the event's Debrief milestone ended (the actual end, falling back to the scheduled end), or
/// null if the event has no Debrief milestone.
/// </param>
public sealed record CrewAssignmentFact(DateTime ShiftEnd, DateTime? DebriefEnd);

/// <summary>
/// FR-36: a casual crew account is deactivated once the work is over, so people who have left do not
/// keep access to venue and event details (NFR-21).
/// </summary>
public static class CrewExpiry
{
    /// <summary>
    /// True when the account should be deactivated: it holds only the casual crew role, it has worked at
    /// least one event, none of its shifts are still ahead, and every event it was assigned to had its
    /// Debrief end more than <paramref name="grace"/> ago.
    /// </summary>
    public static bool ShouldDeactivate(
        bool holdsOnlyCasualCrewRole,
        IReadOnlyCollection<CrewAssignmentFact> assignments,
        DateTime now,
        TimeSpan grace)
    {
        if (!holdsOnlyCasualCrewRole)
        {
            return false;
        }

        // Someone who has not been assigned anything yet has not finished anything. Without this, a
        // newly created account would be deactivated the first night, before its first shift.
        if (assignments.Count == 0)
        {
            return false;
        }

        // Any shift still ahead keeps the account alive, even if an earlier event is long over.
        if (assignments.Any(a => a.ShiftEnd > now))
        {
            return false;
        }

        // An event with no Debrief milestone cannot be shown to be over, so it keeps the account open.
        return assignments.All(a => a.DebriefEnd is { } debriefEnd && now - debriefEnd > grace);
    }
}
