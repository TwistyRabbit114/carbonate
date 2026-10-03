using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class StockCategoryConfiguration : IEntityTypeConfiguration<StockCategory>
{
    public void Configure(EntityTypeBuilder<StockCategory> b)
    {
        b.HasKey(x => x.CategoryId);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Ref<StockCategory>(nameof(StockCategory.ParentCategoryId));
        b.Ref<Division>(nameof(StockCategory.DivisionId));
    }
}

internal class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> b)
    {
        b.HasKey(x => x.StockItemId);
        b.Property(x => x.Sku).HasMaxLength(50);
        b.Property(x => x.Unit).HasMaxLength(30);
        b.HasIndex(x => x.Sku).IsUnique();
        b.HasIndex(x => x.CategoryId);
        b.ToTable(t => t.HasCheckConstraint("CK_StockItem_Quantities",
            "([ReorderLevel] IS NULL OR [ReorderLevel] >= 0) AND ([ConsumptionPerHundredGuests] IS NULL OR [ConsumptionPerHundredGuests] >= 0)"));
        b.Ref<StockCategory>(nameof(StockItem.CategoryId));
        b.Ref<Supplier>(nameof(StockItem.DefaultSupplierId));
    }
}

internal class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.HasKey(x => x.SupplierId);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.ToTable(t => t.HasCheckConstraint("CK_Supplier_LeadTime", "[LeadTimeDays] >= 0"));
    }
}

internal class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> b)
    {
        b.HasKey(x => x.LocationId);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Address).HasMaxLength(500);
    }
}

internal class InventoryLevelConfiguration : IEntityTypeConfiguration<InventoryLevel>
{
    public void Configure(EntityTypeBuilder<InventoryLevel> b)
    {
        b.HasKey(x => x.InventoryLevelId);
        b.HasIndex(x => new { x.StockItemId, x.LocationId }).IsUnique();
        b.Ref<StockItem>(nameof(InventoryLevel.StockItemId), DeleteBehavior.Cascade);
        b.Ref<StockLocation>(nameof(InventoryLevel.LocationId));
    }
}

internal class EventStockRequirementConfiguration : IEntityTypeConfiguration<EventStockRequirement>
{
    public void Configure(EntityTypeBuilder<EventStockRequirement> b)
    {
        b.HasKey(x => x.RequirementId);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.EventId);
        b.HasIndex(x => x.RequiredByDate);
        b.ToTable(t => t.HasCheckConstraint("CK_EventStockRequirement_Quantities",
            "[QuantityRequired] >= 0 AND [QuantityAllocated] >= 0"));
        b.Ref<Event>(nameof(EventStockRequirement.EventId), DeleteBehavior.Cascade);
        b.Ref<StockItem>(nameof(EventStockRequirement.StockItemId));
    }
}

internal class OrderListConfiguration : IEntityTypeConfiguration<OrderList>
{
    public void Configure(EntityTypeBuilder<OrderList> b)
    {
        b.ClusterOn(x => x.GeneratedAt, x => x.OrderListId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Status, x.RequiredByDate });
        b.ToTable(t =>
        {
            // Separation of duties (FR-30) enforced by the database as well as the service.
            t.HasCheckConstraint("CK_OrderList_Approver",
                "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [GeneratedByUserId]");
            t.HasCheckConstraint("CK_OrderList_Period", "[PeriodEnd] >= [PeriodStart]");
        });
        // A consolidated list spans events, so the event is optional and never cascades.
        b.Ref<Event>(nameof(OrderList.EventId));
        b.Ref<Supplier>(nameof(OrderList.SupplierId));
        b.Ref<AppUser>(nameof(OrderList.GeneratedByUserId));
        b.Ref<AppUser>(nameof(OrderList.ApprovedByUserId));
        b.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(x => x.OrderListId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class OrderListLineConfiguration : IEntityTypeConfiguration<OrderListLine>
{
    public void Configure(EntityTypeBuilder<OrderListLine> b)
    {
        b.HasKey(x => x.LineId);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.OrderListId);
        b.ToTable(t => t.HasCheckConstraint("CK_OrderListLine_Quantity", "[QuantityOrdered] > 0"));
        b.Ref<StockItem>(nameof(OrderListLine.StockItemId));
    }
}

internal class EquipmentAssetConfiguration : IEntityTypeConfiguration<EquipmentAsset>
{
    public void Configure(EntityTypeBuilder<EquipmentAsset> b)
    {
        b.HasKey(x => x.AssetId);
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Condition).HasMaxLength(50);
        b.Property(x => x.Status).HasMaxLength(50);
        b.HasIndex(x => x.SerialNumber).IsUnique();
        b.Ref<StockItem>(nameof(EquipmentAsset.StockItemId));
    }
}

internal class IncidentReportConfiguration : IEntityTypeConfiguration<IncidentReport>
{
    public void Configure(EntityTypeBuilder<IncidentReport> b)
    {
        b.ClusterOn(x => x.ReportedAt, x => x.IncidentId);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        b.Property(x => x.PhotoBlobUri).HasMaxLength(1000);
        b.Property(x => x.PhotoSha256).HasMaxLength(64);
        b.HasIndex(x => x.EventId);
        b.HasIndex(x => x.AssetId);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_IncidentReport_Subject", "[AssetId] IS NOT NULL OR [StockItemId] IS NOT NULL");
            t.HasCheckConstraint("CK_IncidentReport_Quantity", "[Quantity] IS NULL OR [Quantity] > 0");
        });
        b.Ref<Event>(nameof(IncidentReport.EventId), DeleteBehavior.Cascade);
        b.Ref<EquipmentAsset>(nameof(IncidentReport.AssetId));
        b.Ref<StockItem>(nameof(IncidentReport.StockItemId));
        b.Ref<AppUser>(nameof(IncidentReport.ReportedByUserId));
    }
}
