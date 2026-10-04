using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// The two calculations FR-25 specifies, kept apart from the database so they can be tested on their
/// own and so the rule is readable in one place rather than buried in a seeding method.
/// </summary>
public static class TemplateMath
{
    /// <summary>
    /// <c>ceil(quantityPerHundredGuests × packSize / 100)</c>.
    /// </summary>
    /// <remarks>
    /// Rounded up, not to nearest: stock is ordered in whole units, and running short at doors is the
    /// failure the client actually cares about. Over-ordering by one shows up in the reconciliation.
    /// </remarks>
    public static decimal ScaleToPackSize(decimal quantityPerHundredGuests, int packSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(packSize);

        return quantityPerHundredGuests <= 0
            ? 0m
            : Math.Ceiling(quantityPerHundredGuests * packSize / 100m);
    }

    /// <summary>
    /// A card's due time: the matching milestone's scheduled start plus the card's offset.
    /// Null when the card names no milestone, or when the event has no milestone of that type —
    /// a card without a due date is valid, a card due at an invented time is not.
    /// </summary>
    public static DateTime? DueAt(
        MilestoneType? milestoneType,
        int offsetHours,
        IReadOnlyDictionary<MilestoneType, DateTime> milestoneStarts)
    {
        ArgumentNullException.ThrowIfNull(milestoneStarts);

        if (milestoneType is null || !milestoneStarts.TryGetValue(milestoneType.Value, out var start))
        {
            return null;
        }

        return start.AddHours(offsetHours);
    }
}
