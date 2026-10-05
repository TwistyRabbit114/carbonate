using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Stock;

namespace Carbonate.Application.Features.Stock;

/// <summary>FR-24. The catalogue, suppliers, equipment and the templates FR-25 seeds from.</summary>
public sealed class StockCatalogueService(
    IStockRepository stock,
    IAuditService audit,
    IFinancialMasker masker,
    ICurrentUser user) : IStockCatalogueService
{
    public async Task<IReadOnlyList<StockCategoryDto>> ListCategoriesAsync(CancellationToken ct)
    {
        Require(PermissionCodes.StockView);
        return await stock.ListCategoriesAsync(ct);
    }

    public async Task<PagedResult<StockItemDto>> ListItemsAsync(StockItemListQuery query, CancellationToken ct)
    {
        Require(PermissionCodes.StockView);

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);

        var page = await stock.ListItemsAsync(query, ct);

        // StandardUnitCost is $cost. The global filter would catch it too, but masking here means the
        // service never hands an unmasked value to anything downstream.
        masker.Mask(page, user);
        return page;
    }

    public async Task<StockItemDto> CreateItemAsync(SaveStockItemRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);
        await ValidateItemAsync(request, null, ct);

        var item = new StockItem
        {
            CategoryId = request.CategoryId,
            DefaultSupplierId = request.DefaultSupplierId,
            Sku = request.Sku.Trim(),
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            IsConsumable = request.IsConsumable,
            IsAsset = request.IsAsset,
            ReorderLevel = request.ReorderLevel,
            ConsumptionPerHundredGuests = request.ConsumptionPerHundredGuests,
            StandardUnitCost = request.StandardUnitCost,
            IsActive = request.IsActive,
        };

        stock.AddItem(item);
        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.item_create", nameof(StockItem), item.StockItemId.ToString(),
            null, Snapshot(item), user.UserId, ct);

        return await ItemAsync(item.StockItemId, ct);
    }

    public async Task<StockItemDto> UpdateItemAsync(
        Guid stockItemId, SaveStockItemRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);

        var item = await stock.FindItemAsync(stockItemId, ct) ?? throw ProblemException.NotFound();
        await ValidateItemAsync(request, stockItemId, ct);

        var before = Snapshot(item);

        item.CategoryId = request.CategoryId;
        item.DefaultSupplierId = request.DefaultSupplierId;
        item.Sku = request.Sku.Trim();
        item.Name = request.Name.Trim();
        item.Unit = request.Unit.Trim();
        item.IsConsumable = request.IsConsumable;
        item.IsAsset = request.IsAsset;
        item.ReorderLevel = request.ReorderLevel;
        item.ConsumptionPerHundredGuests = request.ConsumptionPerHundredGuests;
        item.IsActive = request.IsActive;

        // A caller who cannot see $cost sends it as null, because it was left out of the response they
        // read. Writing that null would destroy the stored cost, so an absent value means "leave it"
        // rather than "clear it". Only someone who can see the field may change it.
        if (user.HasPermission(PermissionCodes.FinanceViewInternalCost))
        {
            item.StandardUnitCost = request.StandardUnitCost;
        }

        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.item_update", nameof(StockItem), stockItemId.ToString(),
            before, Snapshot(item), user.UserId, ct);

        return await ItemAsync(stockItemId, ct);
    }

    public async Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct)
    {
        Require(PermissionCodes.StockView);
        return await stock.ListSuppliersAsync(ct);
    }

    public async Task<SupplierDto> CreateSupplierAsync(SaveSupplierRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);
        ValidateSupplier(request);

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            ContactName = request.ContactName?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            LeadTimeDays = request.LeadTimeDays,
            IsLiquorSupplier = request.IsLiquorSupplier,
            IsActive = request.IsActive,
        };

        stock.AddSupplier(supplier);
        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.supplier_create", nameof(Supplier), supplier.SupplierId.ToString(),
            null, new { supplier.Name, supplier.LeadTimeDays }, user.UserId, ct);

        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(
        Guid supplierId, SaveSupplierRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);
        ValidateSupplier(request);

        var supplier = await stock.FindSupplierAsync(supplierId, ct) ?? throw ProblemException.NotFound();
        var before = new { supplier.Name, supplier.LeadTimeDays, supplier.IsActive };

        supplier.Name = request.Name.Trim();
        supplier.ContactName = request.ContactName?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.LeadTimeDays = request.LeadTimeDays;
        supplier.IsLiquorSupplier = request.IsLiquorSupplier;
        supplier.IsActive = request.IsActive;

        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.supplier_update", nameof(Supplier), supplierId.ToString(),
            before, new { supplier.Name, supplier.LeadTimeDays, supplier.IsActive }, user.UserId, ct);

        return ToDto(supplier);
    }

    public async Task<IReadOnlyList<EquipmentAssetDto>> ListEquipmentAsync(CancellationToken ct)
    {
        Require(PermissionCodes.StockView);
        return await stock.ListEquipmentAsync(ct);
    }

    public async Task<EquipmentAssetDto> CreateEquipmentAsync(
        SaveEquipmentAssetRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);
        await ValidateEquipmentAsync(request, null, ct);

        var asset = new EquipmentAsset
        {
            StockItemId = request.StockItemId,
            SerialNumber = request.SerialNumber.Trim(),
            Condition = request.Condition.Trim(),
            Status = request.Status.Trim(),
            PurchaseDate = request.PurchaseDate,
        };

        stock.AddEquipment(asset);
        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.equipment_create", nameof(EquipmentAsset), asset.AssetId.ToString(),
            null, new { asset.SerialNumber, asset.Status }, user.UserId, ct);

        return await EquipmentAsync(asset.AssetId, ct);
    }

    public async Task<EquipmentAssetDto> UpdateEquipmentAsync(
        Guid assetId, SaveEquipmentAssetRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);

        var asset = await stock.FindEquipmentAsync(assetId, ct) ?? throw ProblemException.NotFound();
        await ValidateEquipmentAsync(request, assetId, ct);

        var before = new { asset.SerialNumber, asset.Condition, asset.Status };

        asset.StockItemId = request.StockItemId;
        asset.SerialNumber = request.SerialNumber.Trim();
        asset.Condition = request.Condition.Trim();
        asset.Status = request.Status.Trim();
        asset.PurchaseDate = request.PurchaseDate;

        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.equipment_update", nameof(EquipmentAsset), assetId.ToString(),
            before, new { asset.SerialNumber, asset.Condition, asset.Status }, user.UserId, ct);

        return await EquipmentAsync(assetId, ct);
    }

    public async Task<IReadOnlyList<ChecklistTemplateDto>> ListTemplatesAsync(CancellationToken ct)
    {
        Require(PermissionCodes.StockManage);
        return await stock.ListTemplatesAsync(ct);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private void Require(string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }

    private async Task<StockItemDto> ItemAsync(Guid stockItemId, CancellationToken ct)
    {
        var dto = await stock.GetItemAsync(stockItemId, ct) ?? throw ProblemException.NotFound();
        masker.Mask(dto, user);
        return dto;
    }

    private async Task<EquipmentAssetDto> EquipmentAsync(Guid assetId, CancellationToken ct)
    {
        var all = await stock.ListEquipmentAsync(ct);
        return all.FirstOrDefault(a => a.AssetId == assetId) ?? throw ProblemException.NotFound();
    }

    private async Task ValidateItemAsync(SaveStockItemRequest request, Guid? exceptId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var references = await stock.CheckItemReferencesAsync(request.CategoryId, request.DefaultSupplierId, ct);

        if (!references.CategoryExists)
        {
            errors["categoryId"] = ["That category does not exist."];
        }

        if (request.DefaultSupplierId is not null && !references.SupplierUsable)
        {
            errors["defaultSupplierId"] = ["That supplier does not exist or is not active."];
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            errors["sku"] = ["Give the item a SKU."];
        }
        else if (await stock.SkuExistsAsync(request.Sku.Trim(), exceptId, ct))
        {
            errors["sku"] = ["That SKU is already in use."];
        }

        if (request.ReorderLevel is < 0)
        {
            errors["reorderLevel"] = ["A reorder level cannot be negative."];
        }

        if (request.ConsumptionPerHundredGuests is < 0)
        {
            errors["consumptionPerHundredGuests"] = ["Consumption cannot be negative."];
        }

        if (request.StandardUnitCost is < 0)
        {
            errors["standardUnitCost"] = ["A cost cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }
    }

    private static void ValidateSupplier(SaveSupplierRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Give the supplier a name."];
        }

        if (request.LeadTimeDays < 0)
        {
            errors["leadTimeDays"] = ["A lead time cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }
    }

    private async Task ValidateEquipmentAsync(
        SaveEquipmentAssetRequest request, Guid? exceptId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (await stock.FindItemAsync(request.StockItemId, ct) is null)
        {
            errors["stockItemId"] = ["That stock item does not exist."];
        }

        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            errors["serialNumber"] = ["Give the asset a serial number."];
        }
        else if (await stock.SerialNumberExistsAsync(request.SerialNumber.Trim(), exceptId, ct))
        {
            errors["serialNumber"] = ["That serial number is already recorded."];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }
    }

    private static SupplierDto ToDto(Supplier s) => new()
    {
        SupplierId = s.SupplierId,
        Name = s.Name,
        ContactName = s.ContactName,
        Email = s.Email,
        Phone = s.Phone,
        LeadTimeDays = s.LeadTimeDays,
        IsLiquorSupplier = s.IsLiquorSupplier,
        IsActive = s.IsActive,
    };

    /// <summary>No cost in the audit snapshot: audit rows are not masked when read back.</summary>
    private static object Snapshot(StockItem i) => new
    {
        i.Sku,
        i.Name,
        i.Unit,
        i.CategoryId,
        i.DefaultSupplierId,
        i.IsConsumable,
        i.IsAsset,
        i.ReorderLevel,
        i.ConsumptionPerHundredGuests,
        i.IsActive,
    };
}
