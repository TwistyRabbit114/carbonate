using System.Globalization;
using System.Text.Json.Serialization;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// A problem with a planned stock line, attached to the line rather than blocking the save.
/// </summary>
/// <remarks>
/// Warnings, not errors, on purpose: the planner may know something the system does not — a second
/// supplier, stock already on site, a client bringing their own. The system's job is to make sure
/// nobody finds out at doors.
/// </remarks>
/// <param name="Code">Stable, for the SPA to key off: <c>SHORTFALL</c> or <c>LEAD_TIME</c>.</param>
public sealed record StockWarning(string Code, string Message)
{
    [JsonExtensionData]
    public Dictionary<string, object?> Details { get; init; } = [];
}

public static class StockWarningCodes
{
    /// <summary>Less planned than this many guests are expected to get through (FR-27).</summary>
    public const string Shortfall = "SHORTFALL";

    /// <summary>The supplier cannot deliver in time for load-in (FR-29).</summary>
    public const string LeadTime = "LEAD_TIME";
}

/// <summary>
/// The two warning rules, kept pure so every boundary can be tested without a database.
/// </summary>
public static class StockRules
{
    /// <summary>
    /// How much of an item this many guests are expected to get through:
    /// <c>ConsumptionPerHundredGuests × packSize / 100</c>.
    /// </summary>
    /// <remarks>
    /// Not rounded. This is a forecast to compare against, not a quantity to order — rounding it up
    /// would make a line that exactly meets expectations look short.
    /// </remarks>
    public static decimal? Expected(decimal? consumptionPerHundredGuests, int packSize)
    {
        if (consumptionPerHundredGuests is null or <= 0 || packSize <= 0)
        {
            // No consumption figure means no opinion. An item nobody has measured must not generate
            // a warning on every event (and most do not — a mobile bar is not consumed).
            return null;
        }

        return consumptionPerHundredGuests.Value * packSize / 100m;
    }

    /// <summary>
    /// FR-27. Warns when less is planned than the guests are expected to get through, while load-in
    /// is still ahead.
    /// </summary>
    /// <param name="packSize"><c>PackSizeActual ?? PackSizeEstimated</c>.</param>
    /// <param name="loadInAt">Null when the event has no load-in milestone yet.</param>
    /// <remarks>
    /// Silent once load-in has passed: the truck has gone, so the warning is no longer something
    /// anyone can act on, and a screen full of warnings about finished events hides the live ones.
    /// </remarks>
    public static StockWarning? Shortfall(
        decimal quantityRequired,
        decimal? consumptionPerHundredGuests,
        int packSize,
        DateTime? loadInAt,
        DateTime now)
    {
        if (loadInAt is not null && loadInAt <= now)
        {
            return null;
        }

        var expected = Expected(consumptionPerHundredGuests, packSize);
        if (expected is null || quantityRequired >= expected)
        {
            return null;
        }

        return new StockWarning(
            StockWarningCodes.Shortfall,
            $"Planned {quantityRequired:0.##}, but {packSize} guests are expected to use about {expected:0.##}.")
        {
            Details = { ["expected"] = expected, ["planned"] = quantityRequired },
        };
    }

    /// <summary>
    /// FR-29. Warns when ordering today would arrive after it is needed:
    /// <c>today + LeadTimeDays &gt; RequiredByDate</c>.
    /// </summary>
    /// <param name="leadTimeDays">Null when the item has no default supplier.</param>
    public static StockWarning? LeadTime(DateOnly requiredByDate, int? leadTimeDays, DateOnly today)
    {
        if (leadTimeDays is null or < 0)
        {
            return null;
        }

        var earliestArrival = today.AddDays(leadTimeDays.Value);
        if (earliestArrival <= requiredByDate)
        {
            return null;
        }

        // Invariant, not the server's locale: these dates are read by the SPA and by tests, and a
        // machine-readable value that changes with a culture setting is a bug waiting for a new region.
        var arrival = earliestArrival.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var needed = requiredByDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new StockWarning(
            StockWarningCodes.LeadTime,
            $"The supplier needs {leadTimeDays} days, so an order placed today arrives "
            + $"{arrival} — after it is needed on {needed}.")
        {
            Details = { ["leadTimeDays"] = leadTimeDays, ["requiredBy"] = needed },
        };
    }
}
