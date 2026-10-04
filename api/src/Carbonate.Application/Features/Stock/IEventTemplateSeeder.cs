using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// Fills a newly created event with its board and its expected stock (FR-25).
/// </summary>
/// <remarks>
/// The events module calls this inside its own transaction, so this contract never saves: it adds
/// to the change tracker and the caller decides whether the whole event create commits. That keeps
/// a half-built event out of the database if anything downstream fails.
/// </remarks>
public interface IEventTemplateSeeder
{
    /// <summary>
    /// Applies the active template for this division, event type and <c>BoardType='Event'</c>.
    /// Does nothing if no template matches — a missing template is a configuration gap, not a
    /// reason to refuse the event.
    /// </summary>
    /// <param name="eventId">The event, which must already be tracked or saved with its milestones.</param>
    /// <param name="eventType">Decides which template applies.</param>
    /// <param name="divisionId">Decides which template applies.</param>
    /// <param name="packSize">
    /// Guests to plan for: <c>PackSizeActual ?? PackSizeEstimated</c>. Scales every stock line.
    /// </param>
    /// <param name="createdByUserId">
    /// Who the generated cards are attributed to — the person creating the event. TASK_CARD
    /// .CreatedByUserId is not nullable (FR-20), so the seeder cannot invent one.
    /// </param>
    Task<TemplateSeedResult> SeedAsync(
        Guid eventId,
        EventType eventType,
        Guid divisionId,
        int packSize,
        Guid createdByUserId,
        CancellationToken ct = default);
}

/// <summary>What the seeder did, so the caller can log it and the tests can assert on it.</summary>
/// <param name="TemplateId">Null when no active template matched.</param>
public readonly record struct TemplateSeedResult(
    Guid? TemplateId,
    int ColumnsCreated,
    int CardsCreated,
    int StockRequirementsCreated,
    IReadOnlyList<string> UnknownSkus)
{
    public static TemplateSeedResult NoTemplate { get; } = new(null, 0, 0, 0, []);
}
