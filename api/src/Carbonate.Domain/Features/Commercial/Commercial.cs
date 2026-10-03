using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Commercial;

public class Quote
{
    public Guid QuoteId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid? CopiedFromQuoteId { get; set; }
    public int Version { get; set; } = 1;
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public decimal SubtotalExVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalIncVat { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<QuoteLine> Lines { get; set; } = [];
}

public class QuoteLine
{
    public Guid QuoteLineId { get; set; } = Guid.NewGuid();
    public Guid QuoteId { get; set; }
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCostToUs { get; set; }
    public decimal UnitPriceToClient { get; set; }
    public decimal LineTotal { get; set; }
    public QuoteLineCategory Category { get; set; } = QuoteLineCategory.Other;
}

public class EventConfirmation
{
    public Guid ConfirmationId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid? DocumentId { get; set; }
    public ConfirmationType ConfirmationType { get; set; }
    public string? ClientPoNumber { get; set; }
    public DateOnly? PoReceivedDate { get; set; }
    public decimal? PoAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public DateOnly? DepositPaidDate { get; set; }
    public string? DepositReference { get; set; }
    public DateTime ConfirmedAt { get; set; }
}

public class Invoice
{
    public Guid InvoiceId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid ConfirmationId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateOnly IssuedDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AmountIncVat { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateOnly? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Reconciliation
{
    public Guid ReconciliationId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateOnly? ReconciledOn { get; set; }
    public decimal? RevenueFromClient { get; set; }
    public decimal? DirectCostToUs { get; set; }
    public decimal? ProfitRetained { get; set; }
    public decimal? MarginPercent { get; set; }
    public decimal? StockVarianceValue { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<ReconciliationLine> Lines { get; set; } = [];
}

public class ReconciliationLine
{
    public Guid LineId { get; set; } = Guid.NewGuid();
    public Guid ReconciliationId { get; set; }
    public Guid StockItemId { get; set; }
    public decimal QuantityIssued { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal QuantityWasted { get; set; }
    public decimal? UnitCostToUs { get; set; }
    public decimal? UnitPriceToClient { get; set; }
    public decimal? LineRevenue { get; set; }
    public decimal? LineProfit { get; set; }
}
