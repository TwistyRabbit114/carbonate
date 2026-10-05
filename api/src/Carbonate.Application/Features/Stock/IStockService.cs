using Carbonate.Application.Common;

namespace Carbonate.Application.Features.Stock;

/// <summary>The catalogue, suppliers, equipment and checklist templates (FR-24).</summary>
public interface IStockCatalogueService
{
    Task<IReadOnlyList<StockCategoryDto>> ListCategoriesAsync(CancellationToken ct);

    Task<PagedResult<StockItemDto>> ListItemsAsync(StockItemListQuery query, CancellationToken ct);

    Task<StockItemDto> CreateItemAsync(SaveStockItemRequest request, CancellationToken ct);

    Task<StockItemDto> UpdateItemAsync(Guid stockItemId, SaveStockItemRequest request, CancellationToken ct);

    Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct);

    Task<SupplierDto> CreateSupplierAsync(SaveSupplierRequest request, CancellationToken ct);

    Task<SupplierDto> UpdateSupplierAsync(Guid supplierId, SaveSupplierRequest request, CancellationToken ct);

    Task<IReadOnlyList<EquipmentAssetDto>> ListEquipmentAsync(CancellationToken ct);

    Task<EquipmentAssetDto> CreateEquipmentAsync(SaveEquipmentAssetRequest request, CancellationToken ct);

    Task<EquipmentAssetDto> UpdateEquipmentAsync(Guid assetId, SaveEquipmentAssetRequest request, CancellationToken ct);

    Task<IReadOnlyList<ChecklistTemplateDto>> ListTemplatesAsync(CancellationToken ct);
}

/// <summary>An event's stock plan, with the shortfall and lead-time warnings (FR-26, FR-27, FR-29).</summary>
public interface IStockRequirementService
{
    Task<IReadOnlyList<StockRequirementDto>> GetAsync(Guid eventId, CancellationToken ct);

    /// <summary>
    /// Replaces the event's plan with what was sent. Lines with a known <c>RequirementId</c> are
    /// updated, new ones added, and anything left out is removed.
    /// </summary>
    Task<IReadOnlyList<StockRequirementDto>> SaveAsync(
        Guid eventId, SaveStockRequirementsRequest request, CancellationToken ct);
}

/// <summary>Consolidated order lists and their approval (FR-28, FR-30).</summary>
public interface IOrderListService
{
    Task<GenerateOrderListsResponse> GenerateAsync(GenerateOrderListsRequest request, CancellationToken ct);

    Task<PagedResult<OrderListDto>> ListAsync(OrderListQuery query, CancellationToken ct);

    Task<OrderListDto> GetAsync(Guid orderListId, CancellationToken ct);

    Task<OrderListDto> SubmitAsync(Guid orderListId, OrderListActionRequest request, CancellationToken ct);

    /// <summary>The approver must hold <c>order.approve</c> and must not be the generator (FR-30).</summary>
    Task<OrderListDto> ApproveAsync(Guid orderListId, OrderListActionRequest request, CancellationToken ct);

    Task<OrderListDto> MarkPlacedAsync(Guid orderListId, OrderListActionRequest request, CancellationToken ct);
}
