using System.Text.Json;
using System.Text.Json.Serialization;
using Carbonate.Application.Features.Stock;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Seeding;

/// <summary>
/// The checklist templates FR-25 seeds new events from. This is configuration, not demo data: without
/// a template every new event starts with an empty board, so it runs in every environment.
/// </summary>
/// <remarks>
/// Adds only what is missing, keyed on (division, name), so running it twice changes nothing and an
/// edit the client makes in the UI is never overwritten.
/// </remarks>
public sealed class TemplateSeeder(CemDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var divisions = await db.Divisions.ToDictionaryAsync(d => d.Code, d => d.DivisionId, ct);
        if (divisions.Count == 0)
        {
            // Divisions come from PlatformSeeder. Nothing to hang templates off yet.
            return;
        }

        var existing = await db.ChecklistTemplates
            .Select(t => new { t.DivisionId, t.Name })
            .ToListAsync(ct);

        var seen = existing.Select(t => (t.DivisionId, t.Name)).ToHashSet();

        foreach (var (divisionCode, name, definition) in Templates())
        {
            if (!divisions.TryGetValue(divisionCode, out var divisionId) || seen.Contains((divisionId, name)))
            {
                continue;
            }

            db.ChecklistTemplates.Add(new ChecklistTemplate
            {
                DivisionId = divisionId,
                Name = name,
                BoardType = BoardType.Event,
                DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions),
                IsActive = true,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static IEnumerable<(string DivisionCode, string Name, TemplateDefinition Definition)> Templates()
    {
        yield return ("CE", "Standard event run sheet", StandardEventTemplate());
        yield return ("CE", "Wedding run sheet", WeddingTemplate());
        yield return ("CLM", "Logistics run sheet", LogisticsTemplate());
    }

    /// <summary>
    /// The three columns every Carbon board uses, matching the admin board (plan section 6) so crew
    /// see the same vocabulary wherever they are.
    /// </summary>
    private static List<TemplateColumn> StandardColumns() =>
    [
        new() { Name = "Assigned", Position = 0 },
        new() { Name = "In Progress / Needs Review", Position = 1 },
        new() { Name = "Complete", Position = 2, IsDoneColumn = true },
    ];

    /// <summary>
    /// Applies to any event type the division does not have a more specific template for. Offsets are
    /// negative hours before the named milestone — "confirm the access route two days before load-in".
    /// </summary>
    private static TemplateDefinition StandardEventTemplate() => new()
    {
        EventTypes = [],
        Columns = StandardColumns(),
        Cards =
        [
            Card("Confirm venue access route and loading bay", "Assigned", MilestoneType.LoadIn, -168, CardPriority.High),
            Card("Book transport and confirm driver details", "Assigned", MilestoneType.LoadIn, -120),
            Card("Confirm crew roster and shift times", "Assigned", MilestoneType.LoadIn, -72, CardPriority.High),
            Card("Pull stock and check against the requirement list", "Assigned", MilestoneType.LoadIn, -48),
            Card("Health and safety file submitted to the venue", "Assigned", MilestoneType.LoadIn, -48, CardPriority.High),
            Card("Load the truck", "Assigned", MilestoneType.LoadIn, 0),
            Card("Bar set-up complete and stocked", "Assigned", MilestoneType.Doors, -2, CardPriority.Critical),
            Card("Ice delivery received and stored", "Assigned", MilestoneType.Doors, -3, CardPriority.High),
            Card("Strike and pack down", "Assigned", MilestoneType.Strike, 0),
            Card("Count returns and log breakages", "Assigned", MilestoneType.LoadOut, 0, CardPriority.High),
            Card("Debrief notes circulated", "Assigned", MilestoneType.Debrief, 0),
            Card("Stock reconciliation submitted", "Assigned", MilestoneType.Reconciliation, 0, CardPriority.High),
        ],
        DefaultStockRequirements =
        [
            // TODO(plan): Appendix D says to use Carbon's real figures from the Head of Logistics and
            // the Bookkeeper, and not to guess. These are PLACEHOLDERS, flagged in docs/seed-data.md.
            Requirement("CUP-500", 220m, SourceMode.Stock),
            Requirement("ICE-BULK-KG", 45m, SourceMode.Order),
            Requirement("SPIRIT-MIX", 8m, SourceMode.Order),
            Requirement("GLASS-WINE", 130m, SourceMode.Stock),
        ],
    };

    private static TemplateDefinition WeddingTemplate() => new()
    {
        EventTypes = [EventType.Wedding],
        Columns = StandardColumns(),
        Cards =
        [
            Card("Recce with the couple and the venue coordinator", "Assigned", MilestoneType.SiteVisit, 0, CardPriority.High),
            Card("Confirm the service schedule with the planner", "Assigned", MilestoneType.LoadIn, -240, CardPriority.High),
            Card("Confirm venue access route and loading bay", "Assigned", MilestoneType.LoadIn, -168),
            Card("Confirm crew roster and dress code", "Assigned", MilestoneType.LoadIn, -72),
            Card("Pull glassware and count against the requirement list", "Assigned", MilestoneType.LoadIn, -48, CardPriority.High),
            Card("Rehearsal walk-through with the crew lead", "Assigned", MilestoneType.Rehearsal, 0),
            Card("Bar set-up complete and stocked", "Assigned", MilestoneType.Doors, -2, CardPriority.Critical),
            Card("Strike quietly — guests may still be on site", "Assigned", MilestoneType.Strike, 0, CardPriority.High),
            Card("Count returns and log breakages", "Assigned", MilestoneType.LoadOut, 0),
            Card("Stock reconciliation submitted", "Assigned", MilestoneType.Reconciliation, 0),
        ],
        DefaultStockRequirements =
        [
            // TODO(plan): placeholders, as above.
            Requirement("GLASS-WINE", 180m, SourceMode.Stock),
            Requirement("GLASS-CHAMP", 110m, SourceMode.Stock),
            Requirement("ICE-BULK-KG", 35m, SourceMode.Order),
            Requirement("SPIRIT-MIX", 6m, SourceMode.Order),
        ],
    };

    private static TemplateDefinition LogisticsTemplate() => new()
    {
        EventTypes = [],
        Columns = StandardColumns(),
        Cards =
        [
            Card("Confirm vehicle and licence plate for the venue", "Assigned", MilestoneType.LoadIn, -120, CardPriority.High),
            Card("Confirm security clearance and sign-in procedure", "Assigned", MilestoneType.LoadIn, -72, CardPriority.High),
            Card("Check infrastructure against the rental list", "Assigned", MilestoneType.LoadIn, -48),
            Card("Load the truck", "Assigned", MilestoneType.LoadIn, 0),
            Card("Infrastructure struck and loaded", "Assigned", MilestoneType.LoadOut, 0),
            Card("Rented infrastructure returned to the supplier", "Assigned", MilestoneType.Debrief, 24),
        ],
        DefaultStockRequirements =
        [
            Requirement("BAR-MOBILE", 1m, SourceMode.Rent),
        ],
    };

    private static TemplateCard Card(
        string subject,
        string column,
        MilestoneType milestone,
        int offsetHours,
        CardPriority priority = CardPriority.Normal) => new()
    {
        Subject = subject,
        ColumnName = column,
        MilestoneType = milestone,
        OffsetHours = offsetHours,
        Priority = priority,
    };

    private static TemplateStockRequirement Requirement(string sku, decimal perHundred, SourceMode mode) => new()
    {
        Sku = sku,
        QuantityPerHundredGuests = perHundred,
        SourceMode = mode,
    };
}
