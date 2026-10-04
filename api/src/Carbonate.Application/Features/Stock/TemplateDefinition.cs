using System.Text.Json.Serialization;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// The shape of <c>CHECKLIST_TEMPLATE.DefinitionJson</c>. One template describes the board a new
/// event starts with and the stock it is expected to need, so creating an event is a single call
/// rather than twenty clicks (FR-25, NFR-03).
/// </summary>
/// <remarks>
/// Stored as JSON rather than tables because the client edits these per division and event type and
/// we do not want a migration every time they change a column name. The database enforces
/// <c>ISJSON() = 1</c>; this type is the contract for what is inside.
/// </remarks>
public sealed class TemplateDefinition
{
    /// <summary>
    /// The event types this template applies to. Empty means it applies to any type, which is how a
    /// division gets one general template instead of six near-identical ones.
    /// </summary>
    /// <remarks>
    /// This lives in the JSON rather than a CHECKLIST_TEMPLATE column because adding a column is a
    /// schema change C owns, and a division has a handful of templates — few enough to match in
    /// memory without a index on it.
    /// </remarks>
    [JsonPropertyName("eventTypes")]
    public List<EventType> EventTypes { get; set; } = [];

    [JsonPropertyName("columns")]
    public List<TemplateColumn> Columns { get; set; } = [];

    [JsonPropertyName("cards")]
    public List<TemplateCard> Cards { get; set; } = [];

    [JsonPropertyName("defaultStockRequirements")]
    public List<TemplateStockRequirement> DefaultStockRequirements { get; set; } = [];
}

public sealed class TemplateColumn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Left to right. Unique within the template; the database enforces it per board.</summary>
    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("wipLimit")]
    public int? WipLimit { get; set; }

    [JsonPropertyName("isDoneColumn")]
    public bool IsDoneColumn { get; set; }
}

public sealed class TemplateCard
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Must match a <see cref="TemplateColumn.Name"/> in the same template.</summary>
    [JsonPropertyName("columnName")]
    public string ColumnName { get; set; } = "";

    [JsonPropertyName("priority")]
    public CardPriority Priority { get; set; } = CardPriority.Normal;

    /// <summary>
    /// The milestone this card hangs off. Null means the card has no due date — a standing job that
    /// is not tied to a point in the run sheet.
    /// </summary>
    [JsonPropertyName("milestoneType")]
    public MilestoneType? MilestoneType { get; set; }

    /// <summary>
    /// Hours from that milestone's scheduled start. Negative is before, which is the normal case:
    /// "confirm the access route 48 hours before load-in" is -48.
    /// </summary>
    [JsonPropertyName("offsetHours")]
    public int OffsetHours { get; set; }
}

public sealed class TemplateStockRequirement
{
    /// <summary>Matched against <c>StockItem.Sku</c>, which is unique. An unknown SKU is skipped.</summary>
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";

    /// <summary>
    /// How much of this item a hundred guests get through. Scaled by the event's pack size, then
    /// rounded up — you cannot order 4.2 bags of ice.
    /// </summary>
    [JsonPropertyName("quantityPerHundredGuests")]
    public decimal QuantityPerHundredGuests { get; set; }

    [JsonPropertyName("sourceMode")]
    public SourceMode SourceMode { get; set; } = SourceMode.Stock;
}
