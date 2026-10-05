using Carbonate.Application.Common;
using Carbonate.Domain.Features.Stock;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// Data access for the stock catalogue, an event's stock plan and order lists (FR-24–30).
/// </summary>
/// <remarks>
/// Reads return DTOs straight from the query so nothing untagged escapes; writes return tracked
/// entities. Money fields arrive unmasked and the service masks them before returning.
/// </remarks>
public interface IStockRepository
{
    // ---- Catalogue (FR-24) ----------------------------------------------------------------

    Task<IReadOnlyList<StockCategoryDto>> ListCategoriesAsync(CancellationToken ct);

    Task<PagedResult<StockItemDto>> ListItemsAsync(StockItemListQuery query, CancellationToken ct);

    Task<StockItemDto?> GetItemAsync(Guid stockItemId, CancellationToken ct);

    Task<StockItem?> FindItemAsync(Guid stockItemId, CancellationToken ct);

    Task<bool> SkuExistsAsync(string sku, Guid? exceptStockItemId, CancellationToken ct);

    Task<StockItemReferences> CheckItemReferencesAsync(Guid categoryId, Guid? supplierId, CancellationToken ct);

    void AddItem(StockItem item);

    Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct);

    Task<Supplier?> FindSupplierAsync(Guid supplierId, CancellationToken ct);

    void AddSupplier(Supplier supplier);

    Task<IReadOnlyList<EquipmentAssetDto>> ListEquipmentAsync(CancellationToken ct);

    Task<EquipmentAsset?> FindEquipmentAsync(Guid assetId, CancellationToken ct);

    Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? exceptAssetId, CancellationToken ct);

    void AddEquipment(EquipmentAsset asset);

    Task<IReadOnlyList<ChecklistTemplateDto>> ListTemplatesAsync(CancellationToken ct);

    // ---- An event's plan (FR-26, FR-27, FR-29) -----------------------------------------------

    /// <summary>Null when the event does not exist or is not visible to this user.</summary>
    Task<EventPlanningContext?> GetEventContextAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct);

    /// <summary>Everything needed to build the requirement rows and their warnings, in one query.</summary>
    Task<IReadOnlyList<RequirementRow>> ListRequirementsAsync(Guid eventId, CancellationToken ct);

    Task<List<EventStockRequirement>> LoadRequirementsForEditAsync(Guid eventId, CancellationToken ct);

    void AddRequirement(EventStockRequirement requirement);

    void RemoveRequirements(IEnumerable<EventStockRequirement> requirements);

    // ---- Order lists (FR-28, FR-30) ----------------------------------------------------------

    /// <summary>
    /// Every requirement that could be ordered in the period, flattened with its item and supplier.
    /// One set-based query: NFR-08 allows five seconds for a seeded December.
    /// </summary>
    Task<IReadOnlyList<OrderCandidate>> ListOrderCandidatesAsync(DateOnly from, DateOnly to, CancellationToken ct);

    Task<PagedResult<OrderListDto>> ListOrderListsAsync(OrderListQuery query, CancellationToken ct);

    Task<OrderListDto?> GetOrderListAsync(Guid orderListId, CancellationToken ct);

    Task<OrderList?> FindOrderListAsync(Guid orderListId, CancellationToken ct);

    void AddOrderList(OrderList list);

    void ExpectRowVersion(OrderList list, byte[] rowVersion);

    /// <exception cref="ConcurrencyConflictException">Someone else saved first.</exception>
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record StockItemReferences(bool CategoryExists, bool SupplierUsable);

/// <summary>What the warning rules need to know about the event being planned.</summary>
/// <param name="PackSize"><c>PackSizeActual ?? PackSizeEstimated</c>.</param>
/// <param name="LoadInAt">Null when the event has no LoadIn milestone.</param>
public sealed record EventPlanningContext(Guid EventId, int PackSize, DateOnly EventDate, DateTime? LoadInAt);

/// <summary>A requirement joined to the facts its warnings depend on.</summary>
public sealed record RequirementRow(
    Guid RequirementId,
    Guid EventId,
    Guid StockItemId,
    string StockItemName,
    string Unit,
    decimal QuantityRequired,
    decimal QuantityAllocated,
    DateOnly RequiredByDate,
    Domain.Common.SourceMode SourceMode,
    string? Notes,
    decimal? ConsumptionPerHundredGuests,
    int? SupplierLeadTimeDays);
