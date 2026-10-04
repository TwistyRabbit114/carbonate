using System.Text.Json.Serialization;
using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Incidents;

public class IncidentDto
{
    public Guid IncidentId { get; set; }
    public Guid EventId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? StockItemId { get; set; }
    public string SubjectName { get; set; } = "";
    public Guid ReportedByUserId { get; set; }
    public string ReportedByName { get; set; } = "";
    public IncidentType IncidentType { get; set; }
    public int? Quantity { get; set; }
    public DateTime ReportedAt { get; set; }
    public string Description { get; set; } = "";
    public string? ResolutionNotes { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ReplacementCost { get; set; }

    /// <summary>A short-lived link to the photo, if one was attached.</summary>
    public string? PhotoUrl { get; set; }
}

public class IncidentListQuery : PageQuery
{
    public Guid? AssetId { get; set; }
}

public class UpdateIncidentRequest
{
    public string? ResolutionNotes { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ReplacementCost { get; set; }
}
