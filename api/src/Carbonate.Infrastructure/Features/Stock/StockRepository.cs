using Carbonate.Application.Common;
using Carbonate.Application.Features.Stock;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Stock;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Stock;

/// <summary>FR-24–30 data access. Projections, not entities, on every read path.</summary>
internal sealed class StockRepository(CemDbContext db) : IStockRepository
{
    // ---- Catalogue --------------------------------------------------------------------------

    public async Task<IReadOnlyList<StockCategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        await db.StockCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new StockCategoryDto
            {
                CategoryId = c.CategoryId,
                ParentCategoryId = c.ParentCategoryId,
                DivisionId = c.DivisionId,
                Name = c.Name,
            })
            .ToListAsync(ct);

    public async Task<PagedResult<StockItemDto>> ListItemsAsync(StockItemListQuery query, CancellationToken ct)
    {
        var items = db.StockItems.AsNoTracking();

        if (query.CategoryId is { } categoryId)
        {
            items = items.Where(i => i.CategoryId == categoryId);
        }

        if (query.DivisionId is { } divisionId)
        {
            items = items.Where(i => db.StockCategories
                .Any(c => c.CategoryId == i.CategoryId && c.DivisionId == divisionId));
        }

        if (query.IsActive is { } isActive)
        {
            items = items.Where(i => i.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            items = items.Where(i => i.Name.Contains(term) || i.Sku.Contains(term));
        }

        var total = await items.CountAsync(ct);

        var page = await items
            .OrderBy(i => i.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => Project(i, db))
            .ToListAsync(ct);

        return new PagedResult<StockItemDto>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
        };
    }

    public async Task<StockItemDto?> GetItemAsync(Guid stockItemId, CancellationToken ct) =>
        await db.StockItems
            .AsNoTracking()
            .Where(i => i.StockItemId == stockItemId)
            .Select(i => Project(i, db))
            .FirstOrDefaultAsync(ct);

    public async Task<StockItem?> FindItemAsync(Guid stockItemId, CancellationToken ct) =>
        await db.StockItems.FirstOrDefaultAsync(i => i.StockItemId == stockItemId, ct);

    public async Task<bool> SkuExistsAsync(string sku, Guid? exceptStockItemId, CancellationToken ct) =>
        await db.StockItems.AnyAsync(
            i => i.Sku == sku && (exceptStockItemId == null || i.StockItemId != exceptStockItemId), ct);

    public async Task<StockItemReferences> CheckItemReferencesAsync(
        Guid categoryId, Guid? supplierId, CancellationToken ct)
    {
        var categoryExists = await db.StockCategories.AnyAsync(c => c.CategoryId == categoryId, ct);
        var supplierUsable = supplierId is null
                             || await db.Suppliers.AnyAsync(s => s.SupplierId == supplierId && s.IsActive, ct);

        return new StockItemReferences(categoryExists, supplierUsable);
    }

    public void AddItem(StockItem item) => db.StockItems.Add(item);

    public async Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct) =>
        await db.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto
            {
                SupplierId = s.SupplierId,
                Name = s.Name,
                ContactName = s.ContactName,
                Email = s.Email,
                Phone = s.Phone,
                LeadTimeDays = s.LeadTimeDays,
                IsLiquorSupplier = s.IsLiquorSupplier,
                IsActive = s.IsActive,
            })
            .ToListAsync(ct);

    public async Task<Supplier?> FindSupplierAsync(Guid supplierId, CancellationToken ct) =>
        await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);

    public void AddSupplier(Supplier supplier) => db.Suppliers.Add(supplier);

    public async Task<IReadOnlyList<EquipmentAssetDto>> ListEquipmentAsync(CancellationToken ct) =>
        await db.EquipmentAssets
            .AsNoTracking()
            .OrderBy(a => a.SerialNumber)
            .Select(a => new EquipmentAssetDto
            {
                AssetId = a.AssetId,
                StockItemId = a.StockItemId,
                StockItemName = db.StockItems
                    .Where(i => i.StockItemId == a.StockItemId)
                    .Select(i => i.Name)
                    .FirstOrDefault() ?? "",
                SerialNumber = a.SerialNumber,
                Condition = a.Condition,
                Status = a.Status,
                PurchaseDate = a.PurchaseDate,
            })
            .ToListAsync(ct);

    public async Task<EquipmentAsset?> FindEquipmentAsync(Guid assetId, CancellationToken ct) =>
        await db.EquipmentAssets.FirstOrDefaultAsync(a => a.AssetId == assetId, ct);

    public async Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? exceptAssetId, CancellationToken ct) =>
        await db.EquipmentAssets.AnyAsync(
            a => a.SerialNumber == serialNumber && (exceptAssetId == null || a.AssetId != exceptAssetId), ct);

    public void AddEquipment(EquipmentAsset asset) => db.EquipmentAssets.Add(asset);

    public async Task<IReadOnlyList<ChecklistTemplateDto>> ListTemplatesAsync(CancellationToken ct) =>
        await db.ChecklistTemplates
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new ChecklistTemplateDto
            {
                TemplateId = t.TemplateId,
                DivisionId = t.DivisionId,
                Name = t.Name,
                BoardType = t.BoardType,
                IsActive = t.IsActive,
            })
            .ToListAsync(ct);

    // ---- An event's plan ---------------------------------------------------------------------

    public async Task<EventPlanningContext?> GetEventContextAsync(
        Guid eventId, Guid? visibleToUserId, CancellationToken ct) =>
        await db.Events
            .AsNoTracking()
            .Where(e => e.EventId == eventId && e.IsActive)
            // Null means the caller sees every event; otherwise only ones they are crewed on.
            .Where(e => visibleToUserId == null
                        || db.CrewAssignments.Any(c => c.EventId == e.EventId && c.UserId == visibleToUserId))
            .Select(e => new EventPlanningContext(
                e.EventId,
                e.PackSizeActual ?? e.PackSizeEstimated,
                e.EventDate,
                db.EventMilestones
                    .Where(m => m.EventId == e.EventId && m.MilestoneType == MilestoneType.LoadIn)
                    .Select(m => (DateTime?)m.ScheduledStart)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RequirementRow>> ListRequirementsAsync(Guid eventId, CancellationToken ct) =>
        await db.EventStockRequirements
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.RequiredByDate)
            .Select(r => new RequirementRow(
                r.RequirementId,
                r.EventId,
                r.StockItemId,
                db.StockItems.Where(i => i.StockItemId == r.StockItemId).Select(i => i.Name).FirstOrDefault() ?? "",
                db.StockItems.Where(i => i.StockItemId == r.StockItemId).Select(i => i.Unit).FirstOrDefault() ?? "",
                r.QuantityRequired,
                r.QuantityAllocated,
                r.RequiredByDate,
                r.SourceMode,
                r.Notes,
                db.StockItems
                    .Where(i => i.StockItemId == r.StockItemId)
                    .Select(i => i.ConsumptionPerHundredGuests)
                    .FirstOrDefault(),
                db.StockItems
                    .Where(i => i.StockItemId == r.StockItemId)
                    .Select(i => i.DefaultSupplierId)
                    .FirstOrDefault() == null
                    ? null
                    : db.Suppliers
                        .Where(s => db.StockItems
                            .Any(i => i.StockItemId == r.StockItemId && i.DefaultSupplierId == s.SupplierId))
                        .Select(s => (int?)s.LeadTimeDays)
                        .FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<List<EventStockRequirement>> LoadRequirementsForEditAsync(
        Guid eventId, CancellationToken ct) =>
        await db.EventStockRequirements.Where(r => r.EventId == eventId).ToListAsync(ct);

    public void AddRequirement(EventStockRequirement requirement) => db.EventStockRequirements.Add(requirement);

    public void RemoveRequirements(IEnumerable<EventStockRequirement> requirements) =>
        db.EventStockRequirements.RemoveRange(requirements);

    // ---- Order lists --------------------------------------------------------------------------

    public async Task<IReadOnlyList<OrderCandidate>> ListOrderCandidatesAsync(
        DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.EventStockRequirements
            .AsNoTracking()
            .Where(r => r.RequiredByDate >= from && r.RequiredByDate <= to)
            .Where(r => r.SourceMode == SourceMode.Order || r.SourceMode == SourceMode.Rent)
            // Only events that are actually happening: an enquiry is not something to order against.
            .Where(r => db.Events.Any(e => e.EventId == r.EventId
                                           && e.IsActive
                                           && (e.Status == EventStatus.ConfirmedInPlanning
                                               || e.Status == EventStatus.InProgress)))
            .Join(db.StockItems, r => r.StockItemId, i => i.StockItemId, (r, i) => new { r, i })
            .GroupJoin(db.Suppliers, x => x.i.DefaultSupplierId, s => s.SupplierId, (x, s) => new { x.r, x.i, s })
            .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => new OrderCandidate(
                x.r.RequirementId,
                x.r.EventId,
                x.i.StockItemId,
                x.i.Sku,
                x.i.Name,
                s == null ? null : s.SupplierId,
                s == null ? null : s.Name,
                s == null ? null : (int?)s.LeadTimeDays,
                x.r.QuantityRequired,
                x.r.RequiredByDate,
                x.r.SourceMode,
                x.i.StandardUnitCost))
            .ToListAsync(ct);

    public async Task<PagedResult<OrderListDto>> ListOrderListsAsync(OrderListQuery query, CancellationToken ct)
    {
        var lists = db.OrderLists.AsNoTracking();

        if (query.Status is { } status)
        {
            lists = lists.Where(l => l.Status == status);
        }

        var total = await lists.CountAsync(ct);

        var page = await lists
            .OrderByDescending(l => l.GeneratedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => Project(l, db))
            .ToListAsync(ct);

        return new PagedResult<OrderListDto>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
        };
    }

    public async Task<OrderListDto?> GetOrderListAsync(Guid orderListId, CancellationToken ct) =>
        await db.OrderLists
            .AsNoTracking()
            .Where(l => l.OrderListId == orderListId)
            .Select(l => Project(l, db))
            .FirstOrDefaultAsync(ct);

    public async Task<OrderList?> FindOrderListAsync(Guid orderListId, CancellationToken ct) =>
        await db.OrderLists.FirstOrDefaultAsync(l => l.OrderListId == orderListId, ct);

    public void AddOrderList(OrderList list) => db.OrderLists.Add(list);

    public void ExpectRowVersion(OrderList list, byte[] rowVersion) =>
        db.Entry(list).Property(l => l.RowVersion).OriginalValue = rowVersion;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }

    // ---- projections --------------------------------------------------------------------------

    private static StockItemDto Project(StockItem i, CemDbContext db) => new()
    {
        StockItemId = i.StockItemId,
        CategoryId = i.CategoryId,
        CategoryName = db.StockCategories
            .Where(c => c.CategoryId == i.CategoryId)
            .Select(c => c.Name)
            .FirstOrDefault() ?? "",
        DefaultSupplierId = i.DefaultSupplierId,
        Sku = i.Sku,
        Name = i.Name,
        Unit = i.Unit,
        IsConsumable = i.IsConsumable,
        IsAsset = i.IsAsset,
        ReorderLevel = i.ReorderLevel,
        ConsumptionPerHundredGuests = i.ConsumptionPerHundredGuests,
        StandardUnitCost = i.StandardUnitCost,
        IsActive = i.IsActive,
    };

    private static OrderListDto Project(OrderList l, CemDbContext db) => new()
    {
        OrderListId = l.OrderListId,
        EventId = l.EventId,
        SupplierId = l.SupplierId,
        SupplierName = db.Suppliers
            .Where(s => s.SupplierId == l.SupplierId)
            .Select(s => s.Name)
            .FirstOrDefault() ?? "",
        GeneratedByUserId = l.GeneratedByUserId,
        Status = l.Status,
        RequiredByDate = l.RequiredByDate,
        GeneratedAt = l.GeneratedAt,
        PeriodStart = l.PeriodStart,
        PeriodEnd = l.PeriodEnd,
        ApprovedByUserId = l.ApprovedByUserId,
        ApprovedAt = l.ApprovedAt,
        PlacedAt = l.PlacedAt,
        RowVersion = Convert.ToBase64String(l.RowVersion),
        Lines = db.OrderListLines
            .Where(line => line.OrderListId == l.OrderListId)
            .OrderBy(line => line.LineId)
            .Select(line => new OrderListLineDto
            {
                LineId = line.LineId,
                StockItemId = line.StockItemId,
                StockItemName = db.StockItems
                    .Where(i => i.StockItemId == line.StockItemId)
                    .Select(i => i.Name)
                    .FirstOrDefault() ?? "",
                Unit = db.StockItems
                    .Where(i => i.StockItemId == line.StockItemId)
                    .Select(i => i.Unit)
                    .FirstOrDefault() ?? "",
                QuantityOrdered = line.QuantityOrdered,
                EstimatedUnitCost = line.EstimatedUnitCost,
                Notes = line.Notes,
            })
            .ToList(),
        // Recomputed on read rather than stored: a lead-time warning depends on today's date, so a
        // value written when the list was generated would go stale the next morning.
        Warnings = new List<StockWarningDto>(),
    };
}
