using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Stock;

/// <summary>One requirement that is a candidate for ordering, flattened out of the database.</summary>
/// <param name="SupplierId">Null when the item has no default supplier.</param>
/// <param name="StandardUnitCost">A <c>$cost</c> value. Never leaves the service unmasked.</param>
public readonly record struct OrderCandidate(
    Guid RequirementId,
    Guid EventId,
    Guid StockItemId,
    string Sku,
    string ItemName,
    Guid? SupplierId,
    string? SupplierName,
    int? SupplierLeadTimeDays,
    decimal QuantityRequired,
    DateOnly RequiredByDate,
    SourceMode SourceMode,
    decimal? StandardUnitCost);

/// <param name="RequiredByDate">The earliest date in the group — the whole order moves to the tightest line.</param>
public sealed record OrderDraft(
    Guid SupplierId,
    string SupplierName,
    int LeadTimeDays,
    DateOnly RequiredByDate,
    IReadOnlyList<OrderDraftLine> Lines,
    IReadOnlyList<StockWarning> Warnings);

public sealed record OrderDraftLine(
    Guid StockItemId,
    string Sku,
    string ItemName,
    decimal QuantityOrdered,
    decimal? EstimatedUnitCost);

/// <param name="Unassigned">
/// Items with no default supplier. A warning for a human to resolve, never a list — an order list with
/// no supplier cannot be sent to anyone (FR-28).
/// </param>
public sealed record OrderGeneration(IReadOnlyList<OrderDraft> Drafts, IReadOnlyList<UnassignedItem> Unassigned);

public sealed record UnassignedItem(Guid StockItemId, string Sku, string ItemName, decimal QuantityRequired);

/// <summary>
/// FR-28. Turns a period's requirements into one draft order per supplier.
/// </summary>
/// <remarks>
/// Pure: the caller does the single set-based query (NFR-08 requires the whole thing under five
/// seconds for a seeded December, so there is no room for a query per line) and this shapes the
/// result. That also makes the grouping and consolidation rules testable on their own.
/// </remarks>
public static class OrderListBuilder
{
    /// <summary>Source modes that mean somebody has to be contacted. <c>Stock</c> comes off the shelf.</summary>
    public static bool IsOrderable(SourceMode mode) => mode is SourceMode.Order or SourceMode.Rent;

    public static OrderGeneration Build(IEnumerable<OrderCandidate> candidates, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var orderable = candidates.Where(c => IsOrderable(c.SourceMode)).ToList();

        var unassigned = orderable
            .Where(c => c.SupplierId is null)
            .GroupBy(c => c.StockItemId)
            .Select(g => new UnassignedItem(
                g.Key,
                g.First().Sku,
                g.First().ItemName,
                g.Sum(c => c.QuantityRequired)))
            .OrderBy(u => u.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var drafts = orderable
            .Where(c => c.SupplierId is not null)
            .GroupBy(c => c.SupplierId!.Value)
            .Select(group => BuildDraft(group.Key, [.. group], today))
            .OrderBy(d => d.SupplierName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OrderGeneration(drafts, unassigned);
    }

    private static OrderDraft BuildDraft(Guid supplierId, List<OrderCandidate> group, DateOnly today)
    {
        // The whole order moves to the tightest line: one delivery, so it has to satisfy the earliest
        // need in the group.
        var requiredBy = group.Min(c => c.RequiredByDate);
        var leadTime = group.Max(c => c.SupplierLeadTimeDays) ?? 0;

        var lines = group
            // Consolidated across events, which is the point of FR-28: three events needing ice in the
            // same week is one order to Coastal Ice, not three.
            .GroupBy(c => c.StockItemId)
            .Select(items => new OrderDraftLine(
                items.Key,
                items.First().Sku,
                items.First().ItemName,
                items.Sum(c => c.QuantityRequired),
                items.Max(c => c.StandardUnitCost)))
            .OrderBy(l => l.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var warnings = new List<StockWarning>();
        var leadTimeWarning = StockRules.LeadTime(requiredBy, leadTime, today);
        if (leadTimeWarning is not null)
        {
            warnings.Add(leadTimeWarning);
        }

        return new OrderDraft(
            supplierId,
            group[0].SupplierName ?? "Unknown supplier",
            leadTime,
            requiredBy,
            lines,
            warnings);
    }
}

/// <summary>FR-30. The states an order list moves through, and who may move it.</summary>
public static class OrderListRules
{
    /// <summary>Strictly forward: <c>Draft → PendingApproval → Approved → Placed</c>.</summary>
    public static bool CanMove(OrderListStatus from, OrderListStatus to) => (from, to) switch
    {
        (OrderListStatus.Draft, OrderListStatus.PendingApproval) => true,
        (OrderListStatus.PendingApproval, OrderListStatus.Approved) => true,
        (OrderListStatus.Approved, OrderListStatus.Placed) => true,
        _ => false,
    };

    /// <summary>
    /// Separation of duties: the person who generated a list may not approve it (FR-30). Enforced here,
    /// in the service, and again by a CHECK constraint on the table.
    /// </summary>
    public static bool CanApprove(Guid generatedByUserId, Guid approverUserId, bool approverHasPermission) =>
        approverHasPermission && approverUserId != generatedByUserId && approverUserId != Guid.Empty;
}
