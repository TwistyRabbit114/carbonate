using System.Text.Json;
using Carbonate.Application.Features.Stock;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Stock;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Features.Stock;

/// <summary>FR-25. See <see cref="IEventTemplateSeeder"/> for the contract and why it does not save.</summary>
internal sealed class EventTemplateSeeder(
    CemDbContext db,
    TimeProvider clock,
    ILogger<EventTemplateSeeder> logger) : IEventTemplateSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task<TemplateSeedResult> SeedAsync(
        Guid eventId,
        EventType eventType,
        Guid divisionId,
        int packSize,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(packSize);

        var (template, definition) = await FindTemplateAsync(divisionId, eventType, ct);
        if (template is null || definition is null)
        {
            // Not an error: a division may genuinely have no template for an event type yet, and
            // refusing the event create would be worse than an empty board.
            logger.LogWarning(
                "No usable active Event template for division {DivisionId} and event type {EventType}; event {EventId} starts with an empty board.",
                divisionId, eventType, eventId);
            return TemplateSeedResult.NoTemplate;
        }

        var board = BuildBoard(eventId, template, definition);
        var cardsCreated = await AddCardsAsync(eventId, board, definition, createdByUserId, ct);
        var (requirementsCreated, unknownSkus) = await AddStockRequirementsAsync(eventId, definition, packSize, ct);

        db.Boards.Add(board);

        return new TemplateSeedResult(
            template.TemplateId,
            board.Columns.Count,
            cardsCreated,
            requirementsCreated,
            unknownSkus);
    }

    /// <summary>
    /// Active Event templates for the division — a handful of rows — then the event type is matched
    /// in memory, because it lives inside DefinitionJson rather than a column (see
    /// <see cref="TemplateDefinition.EventTypes"/>). A template that names the type wins over a
    /// general one, so a division can keep a default and override it for weddings.
    /// </summary>
    private async Task<(ChecklistTemplate? Template, TemplateDefinition? Definition)> FindTemplateAsync(
        Guid divisionId,
        EventType eventType,
        CancellationToken ct)
    {
        var candidates = await db.ChecklistTemplates
            .AsNoTracking()
            .Where(t => t.DivisionId == divisionId && t.BoardType == BoardType.Event && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        (ChecklistTemplate Template, TemplateDefinition Definition)? general = null;

        foreach (var candidate in candidates)
        {
            var definition = Parse(candidate);
            if (definition is null)
            {
                logger.LogError(
                    "Template {TemplateId} ({Name}) has a DefinitionJson this version cannot read; skipping it.",
                    candidate.TemplateId, candidate.Name);
                continue;
            }

            if (definition.EventTypes.Contains(eventType))
            {
                return (candidate, definition);
            }

            // An empty list means "any type". A list that names other types is not a fallback —
            // a wedding template must never be applied to a festival.
            if (definition.EventTypes.Count == 0)
            {
                general ??= (candidate, definition);
            }
        }

        return general is null ? (null, null) : (general.Value.Template, general.Value.Definition);
    }

    private static TemplateDefinition? Parse(ChecklistTemplate template)
    {
        try
        {
            return JsonSerializer.Deserialize<TemplateDefinition>(template.DefinitionJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Board BuildBoard(Guid eventId, ChecklistTemplate template, TemplateDefinition definition)
    {
        var board = new Board
        {
            EventId = eventId,
            TemplateId = template.TemplateId,
            BoardType = BoardType.Event,
            Name = template.Name,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };

        var position = 0;
        foreach (var column in definition.Columns.OrderBy(c => c.Position))
        {
            board.Columns.Add(new BoardColumn
            {
                Name = column.Name,
                // Renumbered from zero rather than trusting the template, so a gap or a duplicate in
                // the JSON cannot violate the unique (BoardId, Position) index.
                Position = position++,
                WipLimit = column.WipLimit,
                IsDoneColumn = column.IsDoneColumn,
            });
        }

        return board;
    }

    private async Task<int> AddCardsAsync(
        Guid eventId,
        Board board,
        TemplateDefinition definition,
        Guid createdByUserId,
        CancellationToken ct)
    {
        if (definition.Cards.Count == 0 || board.Columns.Count == 0)
        {
            return 0;
        }

        // One query for every milestone on the event, then matched in memory. The alternative is a
        // query per card, which is the N+1 the plan rules out (NFR-08).
        var milestones = await db.EventMilestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId)
            .Select(m => new { m.MilestoneType, m.ScheduledStart })
            .ToListAsync(ct);

        var milestoneStarts = milestones
            .GroupBy(m => m.MilestoneType)
            .ToDictionary(g => g.Key, g => g.Min(m => m.ScheduledStart));

        var columnsByName = board.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var fallbackColumn = board.Columns[0];
        var createdAt = clock.GetUtcNow().UtcDateTime;
        var created = 0;

        foreach (var card in definition.Cards)
        {
            var column = columnsByName.TryGetValue(card.ColumnName, out var match) ? match : fallbackColumn;

            column.Cards.Add(new TaskCard
            {
                Subject = card.Subject,
                Description = card.Description,
                Priority = card.Priority,
                DueAt = TemplateMath.DueAt(card.MilestoneType, card.OffsetHours, milestoneStarts),
                Position = column.Cards.Count,
                // Event board cards are Open or Done only, and Done exactly when the column is the
                // done column (B's rule, decision D-009). In practice every seeded card is Open,
                // but deriving it rather than hard-coding keeps the rule in one shape.
                Status = column.IsDoneColumn ? "Done" : "Open",
                CreatedByUserId = createdByUserId,
                CreatedAt = createdAt,
            });
            created++;
        }

        return created;
    }

    private async Task<(int Created, IReadOnlyList<string> UnknownSkus)> AddStockRequirementsAsync(
        Guid eventId,
        TemplateDefinition definition,
        int packSize,
        CancellationToken ct)
    {
        if (definition.DefaultStockRequirements.Count == 0)
        {
            return (0, []);
        }

        var skus = definition.DefaultStockRequirements
            .Select(r => r.Sku)
            .Where(sku => !string.IsNullOrWhiteSpace(sku))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = await db.StockItems
            .AsNoTracking()
            .Where(i => skus.Contains(i.Sku) && i.IsActive)
            .Select(i => new { i.StockItemId, i.Sku })
            .ToListAsync(ct);

        var itemsBySku = items.ToDictionary(i => i.Sku, i => i.StockItemId, StringComparer.OrdinalIgnoreCase);
        var requiredBy = await RequiredByDateAsync(eventId, ct);
        var unknown = new List<string>();
        var created = 0;

        foreach (var line in definition.DefaultStockRequirements)
        {
            if (!itemsBySku.TryGetValue(line.Sku, out var stockItemId))
            {
                // A template that outlives a discontinued SKU should not block an event create.
                unknown.Add(line.Sku);
                continue;
            }

            db.EventStockRequirements.Add(new EventStockRequirement
            {
                EventId = eventId,
                StockItemId = stockItemId,
                QuantityRequired = TemplateMath.ScaleToPackSize(line.QuantityPerHundredGuests, packSize),
                QuantityAllocated = 0,
                RequiredByDate = requiredBy,
                SourceMode = line.SourceMode,
            });
            created++;
        }

        if (unknown.Count > 0)
        {
            logger.LogWarning(
                "Template referenced {Count} SKU(s) with no active stock item: {Skus}.",
                unknown.Count, string.Join(", ", unknown));
        }

        return (created, unknown);
    }

    /// <summary>
    /// Load-in day, which is when stock has to be on the truck. Falls back to the event date when the
    /// event has no LoadIn milestone yet.
    /// </summary>
    private async Task<DateOnly> RequiredByDateAsync(Guid eventId, CancellationToken ct)
    {
        var loadIn = await db.EventMilestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId && m.MilestoneType == MilestoneType.LoadIn)
            .Select(m => (DateTime?)m.ScheduledStart)
            .OrderBy(m => m)
            .FirstOrDefaultAsync(ct);

        if (loadIn is not null)
        {
            return DateOnly.FromDateTime(loadIn.Value);
        }

        var eventDate = await db.Events
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => (DateOnly?)e.EventDate)
            .FirstOrDefaultAsync(ct);

        return eventDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    }
}
