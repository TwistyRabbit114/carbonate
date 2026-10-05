using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Stock;

/// <summary>Catalogue, suppliers, equipment and checklist templates (FR-24).</summary>
[ApiController]
public class StockCatalogueController(IStockCatalogueService catalogue) : ApiControllerBase
{
    [HttpGet("api/stock/categories")]
    [HasPermission(PermissionCodes.StockView)]
    public async Task<ActionResult<IReadOnlyList<StockCategoryDto>>> Categories(CancellationToken ct) =>
        Ok(await catalogue.ListCategoriesAsync(ct));

    [HttpGet("api/stock/items")]
    [HasPermission(PermissionCodes.StockView)]
    public async Task<ActionResult<PagedResult<StockItemDto>>> Items(
        [FromQuery] StockItemListQuery query, CancellationToken ct) =>
        Ok(await catalogue.ListItemsAsync(query, ct));

    [HttpPost("api/stock/items")]
    [HasPermission(PermissionCodes.StockManage)]
    [ProducesResponseType<StockItemDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StockItemDto>> CreateItem(SaveStockItemRequest request, CancellationToken ct)
    {
        var created = await catalogue.CreateItemAsync(request, ct);
        return CreatedAtAction(nameof(Items), new { }, created);
    }

    [HttpPut("api/stock/items/{stockItemId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public async Task<ActionResult<StockItemDto>> UpdateItem(
        Guid stockItemId, SaveStockItemRequest request, CancellationToken ct) =>
        Ok(await catalogue.UpdateItemAsync(stockItemId, request, ct));

    [HttpGet("api/suppliers")]
    [HasPermission(PermissionCodes.StockView)]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> Suppliers(CancellationToken ct) =>
        Ok(await catalogue.ListSuppliersAsync(ct));

    [HttpPost("api/suppliers")]
    [HasPermission(PermissionCodes.StockManage)]
    [ProducesResponseType<SupplierDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(SaveSupplierRequest request, CancellationToken ct)
    {
        var created = await catalogue.CreateSupplierAsync(request, ct);
        return CreatedAtAction(nameof(Suppliers), new { }, created);
    }

    [HttpPut("api/suppliers/{supplierId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(
        Guid supplierId, SaveSupplierRequest request, CancellationToken ct) =>
        Ok(await catalogue.UpdateSupplierAsync(supplierId, request, ct));

    [HttpGet("api/equipment")]
    [HasPermission(PermissionCodes.StockView)]
    public async Task<ActionResult<IReadOnlyList<EquipmentAssetDto>>> Equipment(CancellationToken ct) =>
        Ok(await catalogue.ListEquipmentAsync(ct));

    [HttpPost("api/equipment")]
    [HasPermission(PermissionCodes.StockManage)]
    [ProducesResponseType<EquipmentAssetDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EquipmentAssetDto>> CreateEquipment(
        SaveEquipmentAssetRequest request, CancellationToken ct)
    {
        var created = await catalogue.CreateEquipmentAsync(request, ct);
        return CreatedAtAction(nameof(Equipment), new { }, created);
    }

    [HttpPut("api/equipment/{assetId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public async Task<ActionResult<EquipmentAssetDto>> UpdateEquipment(
        Guid assetId, SaveEquipmentAssetRequest request, CancellationToken ct) =>
        Ok(await catalogue.UpdateEquipmentAsync(assetId, request, ct));

    [HttpGet("api/checklist-templates")]
    [HasPermission(PermissionCodes.StockManage)]
    public async Task<ActionResult<IReadOnlyList<ChecklistTemplateDto>>> Templates(CancellationToken ct) =>
        Ok(await catalogue.ListTemplatesAsync(ct));
}

/// <summary>An event's stock plan, with shortfall and lead-time warnings (FR-26, FR-27, FR-29).</summary>
[ApiController]
public class StockRequirementsController(IStockRequirementService requirements) : ApiControllerBase
{
    [HttpGet("api/events/{eventId:guid}/stock-requirements")]
    [HasPermission(PermissionCodes.StockPlan)]
    public async Task<ActionResult<IReadOnlyList<StockRequirementDto>>> Get(Guid eventId, CancellationToken ct) =>
        Ok(await requirements.GetAsync(eventId, ct));

    [HttpPut("api/events/{eventId:guid}/stock-requirements")]
    [HasPermission(PermissionCodes.StockPlan)]
    public async Task<ActionResult<IReadOnlyList<StockRequirementDto>>> Save(
        Guid eventId, SaveStockRequirementsRequest request, CancellationToken ct) =>
        Ok(await requirements.SaveAsync(eventId, request, ct));
}

/// <summary>Consolidated order lists and their approval (FR-28, FR-30).</summary>
[ApiController]
public class OrderListsController(IOrderListService orders) : ApiControllerBase
{
    /// <summary>Builds one list per supplier for every confirmed requirement due in the period.</summary>
    [HttpPost("api/order-lists/generate")]
    [HasPermission(PermissionCodes.OrderGenerate)]
    public async Task<ActionResult<GenerateOrderListsResponse>> Generate(
        GenerateOrderListsRequest request, CancellationToken ct) =>
        Ok(await orders.GenerateAsync(request, ct));

    [HttpGet("api/order-lists")]
    [HasPermission(PermissionCodes.StockView)]
    public async Task<ActionResult<PagedResult<OrderListDto>>> List(
        [FromQuery] OrderListQuery query, CancellationToken ct) =>
        Ok(await orders.ListAsync(query, ct));

    [HttpGet("api/order-lists/{orderListId:guid}")]
    [HasPermission(PermissionCodes.StockView)]
    public async Task<ActionResult<OrderListDto>> Get(Guid orderListId, CancellationToken ct) =>
        Ok(await orders.GetAsync(orderListId, ct));

    [HttpPost("api/order-lists/{orderListId:guid}/submit")]
    [HasPermission(PermissionCodes.OrderGenerate)]
    public async Task<ActionResult<OrderListDto>> Submit(
        Guid orderListId, OrderListActionRequest request, CancellationToken ct) =>
        Ok(await orders.SubmitAsync(orderListId, request, ct));

    /// <summary>The approver must hold order.approve and must not be the user who generated the list.</summary>
    [HttpPost("api/order-lists/{orderListId:guid}/approve")]
    [HasPermission(PermissionCodes.OrderApprove)]
    public async Task<ActionResult<OrderListDto>> Approve(
        Guid orderListId, OrderListActionRequest request, CancellationToken ct) =>
        Ok(await orders.ApproveAsync(orderListId, request, ct));

    [HttpPost("api/order-lists/{orderListId:guid}/mark-placed")]
    [HasPermission(PermissionCodes.OrderPlace)]
    public async Task<ActionResult<OrderListDto>> MarkPlaced(
        Guid orderListId, OrderListActionRequest request, CancellationToken ct) =>
        Ok(await orders.MarkPlacedAsync(orderListId, request, ct));
}
