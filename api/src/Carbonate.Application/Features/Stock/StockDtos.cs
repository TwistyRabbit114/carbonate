using System.Text.Json.Serialization;
using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Stock;

public class StockCategoryDto
{
    public Guid CategoryId { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public Guid? DivisionId { get; set; }
    public string Name { get; set; } = "";
}

public class StockItemDto
{
    public Guid StockItemId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public Guid? DefaultSupplierId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool IsConsumable { get; set; }
    public bool IsAsset { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? ConsumptionPerHundredGuests { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? StandardUnitCost { get; set; }

    public bool IsActive { get; set; }
}

public class StockItemListQuery : PageQuery
{
    public Guid? CategoryId { get; set; }
    public Guid? DivisionId { get; set; }
    public string? Q { get; set; }
    public bool? IsActive { get; set; }
}

public class SaveStockItemRequest
{
    public Guid CategoryId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool IsConsumable { get; set; }
    public bool IsAsset { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? ConsumptionPerHundredGuests { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? StandardUnitCost { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SupplierDto
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsLiquorSupplier { get; set; }
    public bool IsActive { get; set; }
}

public class SaveSupplierRequest
{
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsLiquorSupplier { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EquipmentAssetDto
{
    public Guid AssetId { get; set; }
    public Guid StockItemId { get; set; }
    public string StockItemName { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly? PurchaseDate { get; set; }
}

public class SaveEquipmentAssetRequest
{
    public Guid StockItemId { get; set; }
    public string SerialNumber { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Status { get; set; } = "";
    public DateOnly? PurchaseDate { get; set; }
}

/// <summary>A warning on a stock line: SHORTFALL (FR-27) or LEAD_TIME (FR-29).</summary>
public class StockWarningDto
{
    public string Code { get; set; } = "";
    public decimal? Expected { get; set; }
    public decimal? Planned { get; set; }
    public int? LeadTimeDays { get; set; }
    public DateOnly? RequiredBy { get; set; }
}

public class StockRequirementDto
{
    public Guid RequirementId { get; set; }
    public Guid EventId { get; set; }
    public Guid StockItemId { get; set; }
    public string StockItemName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public SourceMode SourceMode { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<StockWarningDto> Warnings { get; set; } = [];
}

public class SaveStockRequirementsRequest
{
    public IReadOnlyList<SaveStockRequirement> Items { get; set; } = [];
}

public class SaveStockRequirement
{
    public Guid? RequirementId { get; set; }
    public Guid StockItemId { get; set; }
    public decimal QuantityRequired { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public SourceMode SourceMode { get; set; }
    public string? Notes { get; set; }
}

public class GenerateOrderListsRequest
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public class GenerateOrderListsResponse
{
    /// <summary>One list per supplier (FR-28).</summary>
    public IReadOnlyList<OrderListDto> OrderLists { get; set; } = [];

    /// <summary>Items with no default supplier. They are reported, not turned into a list.</summary>
    public IReadOnlyList<UnassignedSupplierWarningDto> UnassignedSupplier { get; set; } = [];
}

public class UnassignedSupplierWarningDto
{
    public Guid StockItemId { get; set; }
    public string StockItemName { get; set; } = "";
}

public class OrderListDto
{
    public Guid OrderListId { get; set; }

    /// <summary>Null for a consolidated list that spans events.</summary>
    public Guid? EventId { get; set; }

    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public Guid GeneratedByUserId { get; set; }
    public OrderListStatus Status { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PlacedAt { get; set; }
    public string RowVersion { get; set; } = "";
    public IReadOnlyList<OrderListLineDto> Lines { get; set; } = [];
    public IReadOnlyList<StockWarningDto> Warnings { get; set; } = [];
}

public class OrderListLineDto
{
    public Guid LineId { get; set; }
    public Guid StockItemId { get; set; }
    public string StockItemName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal QuantityOrdered { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? EstimatedUnitCost { get; set; }

    public string? Notes { get; set; }
}

public class OrderListQuery : PageQuery
{
    public OrderListStatus? Status { get; set; }
}

/// <summary>Body of submit, approve and mark-placed. The approver must differ from the generator (FR-30).</summary>
public class OrderListActionRequest
{
    public string RowVersion { get; set; } = "";
}

public class ChecklistTemplateDto
{
    public Guid TemplateId { get; set; }
    public Guid DivisionId { get; set; }
    public string Name { get; set; } = "";
    public BoardType BoardType { get; set; }
    public bool IsActive { get; set; }
}
