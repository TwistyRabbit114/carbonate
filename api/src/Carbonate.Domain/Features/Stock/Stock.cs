using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Stock;

public class StockCategory
{
    public Guid CategoryId { get; set; } = Guid.NewGuid();
    public Guid? ParentCategoryId { get; set; }
    public Guid? DivisionId { get; set; }
    public string Name { get; set; } = "";
}

public class StockItem
{
    public Guid StockItemId { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool IsConsumable { get; set; }
    public bool IsAsset { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? ConsumptionPerHundredGuests { get; set; }
    public decimal? StandardUnitCost { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Supplier
{
    public Guid SupplierId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsLiquorSupplier { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockLocation
{
    public Guid LocationId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Address { get; set; }
}

public class InventoryLevel
{
    public Guid InventoryLevelId { get; set; } = Guid.NewGuid();
    public Guid StockItemId { get; set; }
    public Guid LocationId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public DateTime? LastCountedAt { get; set; }
}

public class EventStockRequirement
{
    public Guid RequirementId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid StockItemId { get; set; }
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public SourceMode SourceMode { get; set; }
    public string? Notes { get; set; }
}

public class OrderList
{
    public Guid OrderListId { get; set; } = Guid.NewGuid();
    public Guid? EventId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public OrderListStatus Status { get; set; } = OrderListStatus.Draft;
    public DateOnly RequiredByDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PlacedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<OrderListLine> Lines { get; set; } = [];
}

public class OrderListLine
{
    public Guid LineId { get; set; } = Guid.NewGuid();
    public Guid OrderListId { get; set; }
    public Guid StockItemId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal? EstimatedUnitCost { get; set; }
    public string? Notes { get; set; }
}

public class EquipmentAsset
{
    public Guid AssetId { get; set; } = Guid.NewGuid();
    public Guid StockItemId { get; set; }
    public string SerialNumber { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly? PurchaseDate { get; set; }
}

public class IncidentReport
{
    public Guid IncidentId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? StockItemId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public IncidentType IncidentType { get; set; }
    public int? Quantity { get; set; }
    public DateTime ReportedAt { get; set; }
    public string Description { get; set; } = "";
    public string? ResolutionNotes { get; set; }
    public decimal? ReplacementCost { get; set; }
    public string? PhotoBlobUri { get; set; }
    public string? PhotoSha256 { get; set; }
}
