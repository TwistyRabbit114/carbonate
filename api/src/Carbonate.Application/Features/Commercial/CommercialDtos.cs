using System.Text.Json.Serialization;
using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Commercial;

public class QuoteDto
{
    public Guid QuoteId { get; set; }
    public Guid EventId { get; set; }
    public Guid? CopiedFromQuoteId { get; set; }
    public int Version { get; set; }
    public QuoteStatus Status { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SubtotalExVat { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? VatAmount { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TotalIncVat { get; set; }

    /// <summary>What the lines cost Carbon in total.</summary>
    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? InternalCostTotal { get; set; }

    /// <summary>The actual margin on this costing.</summary>
    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MarginPercent { get; set; }

    /// <summary>The target band for this event's pack size, from configuration (FR-11).</summary>
    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MarginBandDto? TargetMarginBand { get; set; }

    /// <summary>True when the total is above the approval threshold and the Director has not approved it.</summary>
    public bool RequiresApproval { get; set; }

    public DateOnly? ValidUntil { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = "";
    public IReadOnlyList<QuoteLineDto> Lines { get; set; } = [];
}

public class MarginBandDto
{
    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TargetMinPct { get; set; }

    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TargetMaxPct { get; set; }
}

public class QuoteLineDto
{
    public Guid QuoteLineId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public QuoteLineCategory Category { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitCostToUs { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitPriceToClient { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LineTotal { get; set; }
}

public class SaveQuoteRequest
{
    public DateOnly? ValidUntil { get; set; }
    public IReadOnlyList<SaveQuoteLine> Lines { get; set; } = [];

    /// <summary>Required when changing an existing quote; ignored on create. Totals are always recomputed on the server.</summary>
    public string? RowVersion { get; set; }
}

public class SaveQuoteLine
{
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public QuoteLineCategory Category { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitCostToUs { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitPriceToClient { get; set; }
}

/// <summary>Copies the lines, quantities and prices of an earlier costing for the same client (FR-15).</summary>
public class CopyQuoteRequest
{
    public Guid SourceQuoteId { get; set; }
}

/// <summary>The body of submit, approve, issue and accept.</summary>
public class QuoteActionRequest
{
    public string RowVersion { get; set; } = "";
}

public class RecordConfirmationRequest
{
    public ConfirmationType ConfirmationType { get; set; }
    public string? ClientPoNumber { get; set; }
    public DateOnly? PoReceivedDate { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PoAmount { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? DepositAmount { get; set; }

    public DateOnly? DepositPaidDate { get; set; }
    public string? DepositReference { get; set; }
    public Guid? DocumentId { get; set; }
}

public class ConfirmationDto
{
    public Guid ConfirmationId { get; set; }
    public Guid EventId { get; set; }
    public ConfirmationType ConfirmationType { get; set; }
    public string? ClientPoNumber { get; set; }
    public DateOnly? PoReceivedDate { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PoAmount { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? DepositAmount { get; set; }

    public DateOnly? DepositPaidDate { get; set; }
    public string? DepositReference { get; set; }
    public Guid? DocumentId { get; set; }
    public DateTime ConfirmedAt { get; set; }
}

public class InvoiceDto
{
    public Guid InvoiceId { get; set; }
    public Guid EventId { get; set; }
    public string EventCode { get; set; } = "";
    public Guid ConfirmationId { get; set; }

    /// <summary>The PO number or deposit reference this invoice rests on (FR-13).</summary>
    public string ConfirmationReference { get; set; } = "";

    public string InvoiceNumber { get; set; } = "";
    public DateOnly IssuedDate { get; set; }
    public DateOnly DueDate { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AmountIncVat { get; set; }

    public InvoiceStatus Status { get; set; }
    public DateOnly? PaidDate { get; set; }
}

public class InvoiceListQuery : PageQuery
{
    public InvoiceStatus? Status { get; set; }
}

public class CreateInvoiceRequest
{
    public Guid ConfirmationId { get; set; }
    public DateOnly IssuedDate { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AmountIncVat { get; set; }
}

public class UpdateInvoiceRequest
{
    public InvoiceStatus Status { get; set; }
    public DateOnly? PaidDate { get; set; }
}

/// <summary>An event that has been delivered but not invoiced (FR-16, should have).</summary>
public class UninvoicedEventDto
{
    public Guid EventId { get; set; }
    public string EventCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClientName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public int DaysSinceEvent { get; set; }
}

/// <summary>Post-event reconciliation (FR-17, should have).</summary>
public class ReconciliationDto
{
    public Guid ReconciliationId { get; set; }
    public Guid EventId { get; set; }
    public string Status { get; set; } = "";
    public DateOnly? ReconciledOn { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? RevenueFromClient { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? DirectCostToUs { get; set; }

    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ProfitRetained { get; set; }

    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MarginPercent { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? StockVarianceValue { get; set; }

    public string? Notes { get; set; }
    public IReadOnlyList<ReconciliationLineDto> Lines { get; set; } = [];
}

public class ReconciliationLineDto
{
    public Guid StockItemId { get; set; }
    public string StockItemName { get; set; } = "";
    public decimal QuantityIssued { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal QuantityWasted { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitCostToUs { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? UnitPriceToClient { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LineRevenue { get; set; }

    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? LineProfit { get; set; }
}

public class SaveReconciliationRequest
{
    public string? Notes { get; set; }
    public bool Complete { get; set; }
    public IReadOnlyList<SaveReconciliationLine> Lines { get; set; } = [];
}

public class SaveReconciliationLine
{
    public Guid StockItemId { get; set; }
    public decimal QuantityIssued { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal QuantityWasted { get; set; }
}
