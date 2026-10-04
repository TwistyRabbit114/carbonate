namespace Carbonate.Domain.Features.Events;

/// <summary>A milestone as the calculator sees it: an id, a window, and whether it has actually started.</summary>
public sealed record ScheduleItem(Guid Id, DateTime Start, DateTime End, DateTime? ActualStart = null)
{
    public TimeSpan Duration => End - Start;

    /// <summary>A milestone that has actually started never moves.</summary>
    public bool IsFrozen => ActualStart is not null;
}

/// <summary>Finish-to-start: the successor may not start until the predecessor has ended plus the lag.</summary>
public sealed record ScheduleLink(Guid PredecessorId, Guid SuccessorId, TimeSpan Lag);

public enum ScheduleFailure
{
    None,
    UnknownMilestone,
    InvalidWindow,
    FrozenMilestone,
    Cycle,
}

public sealed record ScheduleResult(
    ScheduleFailure Failure,
    string? Message,
    IReadOnlyList<ScheduleItem> Items,
    IReadOnlyList<Guid> Moved)
{
    public bool Succeeded => Failure == ScheduleFailure.None;
}

/// <summary>
/// FR-04: moving one milestone cascades through everything that depends on it. Pure logic with no
/// database access, so the rules can be tested on their own.
/// </summary>
public static class ScheduleCalculator
{
    /// <summary>
    /// Moves <paramref name="movedId"/> to the new window and recalculates the rest:
    /// <list type="number">
    /// <item>Every transitive successor shifts by the same amount as the moved milestone's start,
    /// keeping its own duration.</item>
    /// <item>In dependency order, any milestone that now starts before its predecessors allow
    /// (predecessor end plus lag) is pushed later. Nothing is ever pulled earlier by this step.</item>
    /// <item>A milestone with an actual start never moves. If the change would move one, the whole
    /// reschedule is refused.</item>
    /// <item>A dependency cycle anywhere in the event refuses the reschedule.</item>
    /// </list>
    /// </summary>
    public static ScheduleResult Recalculate(
        IReadOnlyCollection<ScheduleItem> items,
        IReadOnlyCollection<ScheduleLink> links,
        Guid movedId,
        DateTime newStart,
        DateTime newEnd)
    {
        var original = items.ToDictionary(i => i.Id);

        if (!original.TryGetValue(movedId, out var moved))
        {
            return Fail(ScheduleFailure.UnknownMilestone, "That milestone is not part of this event.", items);
        }

        if (newEnd < newStart)
        {
            return Fail(ScheduleFailure.InvalidWindow, "A milestone cannot end before it starts.", items);
        }

        if (links.Any(l => !original.ContainsKey(l.PredecessorId) || !original.ContainsKey(l.SuccessorId)))
        {
            return Fail(ScheduleFailure.UnknownMilestone, "A dependency points at a milestone that is not in this event.", items);
        }

        var predecessors = links.GroupBy(l => l.SuccessorId).ToDictionary(g => g.Key, g => g.ToList());
        var successors = links.GroupBy(l => l.PredecessorId).ToDictionary(g => g.Key, g => g.Select(l => l.SuccessorId).ToList());

        var order = TopologicalOrder(original.Keys, predecessors, successors);
        if (order is null)
        {
            return Fail(ScheduleFailure.Cycle, "The milestone dependencies form a loop, so they cannot be scheduled.", items);
        }

        var windows = items.ToDictionary(i => i.Id, i => (i.Start, i.End));

        // Step 1: the moved milestone takes its new window, and everything downstream shifts with it.
        var shift = newStart - moved.Start;
        windows[movedId] = (newStart, newEnd);
        foreach (var id in Downstream(movedId, successors))
        {
            var (start, end) = windows[id];
            windows[id] = (start + shift, end + shift);
        }

        // Step 2: push later where a predecessor, or another predecessor than the one that moved, now
        // finishes too late. Walking in dependency order means a push cascades in one pass.
        foreach (var id in order)
        {
            if (!predecessors.TryGetValue(id, out var incoming))
            {
                continue;
            }

            var earliest = incoming.Max(l => windows[l.PredecessorId].End + l.Lag);
            var (start, end) = windows[id];
            if (start < earliest)
            {
                var push = earliest - start;
                windows[id] = (start + push, end + push);
            }
        }

        var changed = order
            .Where(id => windows[id] != (original[id].Start, original[id].End))
            .ToList();

        var frozen = changed.FirstOrDefault(id => original[id].IsFrozen);
        if (frozen != Guid.Empty)
        {
            return Fail(
                ScheduleFailure.FrozenMilestone,
                "That change would move a milestone that has already started, which cannot be rescheduled.",
                items);
        }

        var result = items
            .Select(i => i with { Start = windows[i.Id].Start, End = windows[i.Id].End })
            .ToList();

        return new ScheduleResult(ScheduleFailure.None, null, result, changed);
    }

    /// <summary>Kahn's algorithm. Returns null when there is a cycle.</summary>
    private static List<Guid>? TopologicalOrder(
        IEnumerable<Guid> ids,
        Dictionary<Guid, List<ScheduleLink>> predecessors,
        Dictionary<Guid, List<Guid>> successors)
    {
        var remaining = ids.ToDictionary(id => id, id => predecessors.TryGetValue(id, out var p) ? p.Count : 0);
        var ready = new Queue<Guid>(remaining.Where(r => r.Value == 0).Select(r => r.Key));
        var order = new List<Guid>(remaining.Count);

        while (ready.Count > 0)
        {
            var id = ready.Dequeue();
            order.Add(id);

            if (!successors.TryGetValue(id, out var next))
            {
                continue;
            }

            foreach (var successor in next)
            {
                if (--remaining[successor] == 0)
                {
                    ready.Enqueue(successor);
                }
            }
        }

        return order.Count == remaining.Count ? order : null;
    }

    private static HashSet<Guid> Downstream(Guid from, Dictionary<Guid, List<Guid>> successors)
    {
        var seen = new HashSet<Guid>();
        var queue = new Queue<Guid>([from]);

        while (queue.Count > 0)
        {
            if (!successors.TryGetValue(queue.Dequeue(), out var next))
            {
                continue;
            }

            foreach (var id in next.Where(seen.Add))
            {
                queue.Enqueue(id);
            }
        }

        return seen;
    }

    private static ScheduleResult Fail(ScheduleFailure failure, string message, IEnumerable<ScheduleItem> items) =>
        new(failure, message, [.. items], []);
}
