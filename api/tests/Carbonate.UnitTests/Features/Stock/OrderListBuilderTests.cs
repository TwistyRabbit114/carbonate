using Carbonate.Application.Features.Stock;
using Carbonate.Domain.Common;
using Shouldly;

namespace Carbonate.UnitTests.Features.Stock;

/// <summary>FR-28 and FR-30.</summary>
public class OrderListBuilderTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);
    private static readonly Guid CoastalIce = Guid.NewGuid();
    private static readonly Guid VineAndCo = Guid.NewGuid();
    private static readonly Guid IceItem = Guid.NewGuid();
    private static readonly Guid SpiritsItem = Guid.NewGuid();
    private static readonly Guid BarItem = Guid.NewGuid();

    private static OrderCandidate Candidate(
        Guid stockItemId,
        string sku,
        string name,
        Guid? supplierId,
        string? supplierName,
        int? leadTime,
        decimal quantity,
        int daysOut,
        SourceMode mode = SourceMode.Order) =>
        new(Guid.NewGuid(), Guid.NewGuid(), stockItemId, sku, name, supplierId, supplierName, leadTime,
            quantity, Today.AddDays(daysOut), mode, 42.50m);

    private static OrderCandidate Ice(decimal quantity, int daysOut) =>
        Candidate(IceItem, "ICE-BULK-KG", "Ice, bulk", CoastalIce, "Coastal Ice Co.", 5, quantity, daysOut);

    [Fact]
    public void Groups_requirements_by_supplier()
    {
        var result = OrderListBuilder.Build(
        [
            Ice(100m, 20),
            Candidate(SpiritsItem, "SPIRIT-MIX", "Spirits, mixed", VineAndCo, "Vine & Co.", 2, 30m, 20),
        ], Today);

        result.Drafts.Count.ShouldBe(2);
        result.Drafts.Select(d => d.SupplierName).ShouldBe(["Coastal Ice Co.", "Vine & Co."]);
    }

    [Fact]
    public void Consolidates_the_same_item_across_several_events()
    {
        // The point of FR-28: three events needing ice the same week is one order, not three.
        var result = OrderListBuilder.Build([Ice(100m, 20), Ice(250m, 22), Ice(80m, 25)], Today);

        var draft = result.Drafts.ShouldHaveSingleItem();
        var line = draft.Lines.ShouldHaveSingleItem();
        line.QuantityOrdered.ShouldBe(430m);
    }

    [Fact]
    public void Takes_the_earliest_date_in_the_group()
    {
        // One delivery, so it has to satisfy the tightest line in the order.
        var result = OrderListBuilder.Build([Ice(100m, 25), Ice(100m, 12), Ice(100m, 30)], Today);

        result.Drafts.ShouldHaveSingleItem().RequiredByDate.ShouldBe(Today.AddDays(12));
    }

    [Fact]
    public void Items_with_no_supplier_become_a_warning_not_a_list()
    {
        var result = OrderListBuilder.Build(
        [
            Ice(100m, 20),
            Candidate(BarItem, "BAR-MOBILE", "Mobile bar unit", null, null, null, 2m, 20, SourceMode.Rent),
        ], Today);

        // An order list with no supplier cannot be sent to anyone.
        result.Drafts.ShouldHaveSingleItem().SupplierName.ShouldBe("Coastal Ice Co.");
        var unassigned = result.Unassigned.ShouldHaveSingleItem();
        unassigned.Sku.ShouldBe("BAR-MOBILE");
        unassigned.QuantityRequired.ShouldBe(2m);
    }

    [Fact]
    public void Unassigned_items_are_consolidated_too()
    {
        var result = OrderListBuilder.Build(
        [
            Candidate(BarItem, "BAR-MOBILE", "Mobile bar unit", null, null, null, 2m, 20, SourceMode.Rent),
            Candidate(BarItem, "BAR-MOBILE", "Mobile bar unit", null, null, null, 3m, 25, SourceMode.Rent),
        ], Today);

        result.Unassigned.ShouldHaveSingleItem().QuantityRequired.ShouldBe(5m);
    }

    [Theory]
    [InlineData(SourceMode.Order, true)]
    [InlineData(SourceMode.Rent, true)]
    // Stock comes off our own shelf. Nobody needs to be contacted, so it is not on an order list.
    [InlineData(SourceMode.Stock, false)]
    public void Only_ordered_and_rented_items_reach_a_supplier(SourceMode mode, bool included)
    {
        var result = OrderListBuilder.Build(
            [Candidate(IceItem, "ICE-BULK-KG", "Ice, bulk", CoastalIce, "Coastal Ice Co.", 5, 100m, 20, mode)],
            Today);

        result.Drafts.Count.ShouldBe(included ? 1 : 0);
    }

    [Fact]
    public void A_draft_inside_its_lead_time_carries_the_lead_time_warning()
    {
        // Vantage: load-in in 3 days, Coastal Ice needs 5. This is the trap in the demo data.
        var draft = OrderListBuilder.Build([Ice(100m, 3)], Today).Drafts.ShouldHaveSingleItem();

        draft.Warnings.ShouldHaveSingleItem().Code.ShouldBe(StockWarningCodes.LeadTime);
    }

    [Fact]
    public void A_draft_with_room_to_spare_has_no_warnings() =>
        OrderListBuilder.Build([Ice(100m, 30)], Today).Drafts.ShouldHaveSingleItem().Warnings.ShouldBeEmpty();

    [Fact]
    public void Nothing_in_means_nothing_out()
    {
        var result = OrderListBuilder.Build([], Today);

        result.Drafts.ShouldBeEmpty();
        result.Unassigned.ShouldBeEmpty();
    }

    // ---- FR-30, approval ------------------------------------------------------------------

    [Theory]
    [InlineData(OrderListStatus.Draft, OrderListStatus.PendingApproval, true)]
    [InlineData(OrderListStatus.PendingApproval, OrderListStatus.Approved, true)]
    [InlineData(OrderListStatus.Approved, OrderListStatus.Placed, true)]
    // No skipping approval.
    [InlineData(OrderListStatus.Draft, OrderListStatus.Approved, false)]
    [InlineData(OrderListStatus.Draft, OrderListStatus.Placed, false)]
    [InlineData(OrderListStatus.PendingApproval, OrderListStatus.Placed, false)]
    // No going back.
    [InlineData(OrderListStatus.Approved, OrderListStatus.PendingApproval, false)]
    [InlineData(OrderListStatus.Placed, OrderListStatus.Approved, false)]
    [InlineData(OrderListStatus.PendingApproval, OrderListStatus.Draft, false)]
    public void Order_lists_move_strictly_forward(OrderListStatus from, OrderListStatus to, bool allowed) =>
        OrderListRules.CanMove(from, to).ShouldBe(allowed);

    [Fact]
    public void The_person_who_generated_a_list_cannot_approve_it()
    {
        // Separation of duties (FR-30): also a CHECK constraint on the table.
        var generator = Guid.NewGuid();

        OrderListRules.CanApprove(generator, generator, approverHasPermission: true).ShouldBeFalse();
    }

    [Fact]
    public void Someone_else_with_the_permission_can_approve_it() =>
        OrderListRules.CanApprove(Guid.NewGuid(), Guid.NewGuid(), approverHasPermission: true).ShouldBeTrue();

    [Fact]
    public void Someone_else_without_the_permission_cannot() =>
        OrderListRules.CanApprove(Guid.NewGuid(), Guid.NewGuid(), approverHasPermission: false).ShouldBeFalse();
}
