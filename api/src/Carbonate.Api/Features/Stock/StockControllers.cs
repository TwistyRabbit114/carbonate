using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Stock;

/// <summary>Catalogue, suppliers, equipment and checklist templates (FR-24). Stubs until built; D owns the bodies.</summary>
[ApiController]
public class StockCatalogueController : ApiControllerBase
{
    [HttpGet("api/stock/categories")]
    [HasPermission(PermissionCodes.StockView)]
    public ActionResult<IReadOnlyList<StockCategoryDto>> Categories() => NotYetBuilt();

    [HttpGet("api/stock/items")]
    [HasPermission(PermissionCodes.StockView)]
    public ActionResult<PagedResult<StockItemDto>> Items([FromQuery] StockItemListQuery query) => NotYetBuilt();

    [HttpPost("api/stock/items")]
    [HasPermission(PermissionCodes.StockManage)]
    [ProducesResponseType<StockItemDto>(StatusCodes.Status201Created)]
    public ActionResult<StockItemDto> CreateItem(SaveStockItemRequest request) => NotYetBuilt();

    [HttpPut("api/stock/items/{stockItemId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public ActionResult<StockItemDto> UpdateItem(Guid stockItemId, SaveStockItemRequest request) => NotYetBuilt();

    [HttpGet("api/suppliers")]
    [HasPermission(PermissionCodes.StockView)]
    public ActionResult<IReadOnlyList<SupplierDto>> Suppliers() => NotYetBuilt();

    [HttpPost("api/suppliers")]
    [HasPermission(PermissionCodes.StockManage)]
    [ProducesResponseType<SupplierDto>(StatusCodes.Status201Created)]
    public ActionResult<SupplierDto> CreateSupplier(SaveSupplierRequest request) => NotYetBuilt();

    [HttpPut("api/suppliers/{supplierId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public ActionResult<SupplierDto> UpdateSupplier(Guid supplierId, SaveSupplierRequest request) => NotYetBuilt();

    [HttpGet("api/equipment")]
    [HasPermission(PermissionCodes.StockView)]
    public ActionResult<IReadOnlyList<EquipmentAssetDto>> Equipment() => NotYetBuilt();

    [HttpPost("api/equipment")]
    [HasPermission(PermissionCodes.StockManage)]
    [ProducesResponseType<EquipmentAssetDto>(StatusCodes.Status201Created)]
    public ActionResult<EquipmentAssetDto> CreateEquipment(SaveEquipmentAssetRequest request) => NotYetBuilt();

    [HttpPut("api/equipment/{assetId:guid}")]
    [HasPermission(PermissionCodes.StockManage)]
    public ActionResult<EquipmentAssetDto> UpdateEquipment(Guid assetId, SaveEquipmentAssetRequest request) =>
        NotYetBuilt();

    [HttpGet("api/checklist-templates")]
    [HasPermission(PermissionCodes.StockManage)]
    public ActionResult<IReadOnlyList<ChecklistTemplateDto>> Templates() => NotYetBuilt();
}

/// <summary>An event's stock plan, with shortfall and lead-time warnings (FR-26, FR-27, FR-29).</summary>
[ApiController]
public class StockRequirementsController : ApiControllerBase
{
    [HttpGet("api/events/{eventId:guid}/stock-requirements")]
    [HasPermission(PermissionCodes.StockPlan)]
    public ActionResult<IReadOnlyList<StockRequirementDto>> Get(Guid eventId) => NotYetBuilt();

    [HttpPut("api/events/{eventId:guid}/stock-requirements")]
    [HasPermission(PermissionCodes.StockPlan)]
    public ActionResult<IReadOnlyList<StockRequirementDto>> Save(Guid eventId, SaveStockRequirementsRequest request) =>
        NotYetBuilt();
}

/// <summary>Consolidated order lists and their approval (FR-28, FR-30).</summary>
[ApiController]
public class OrderListsController : ApiControllerBase
{
    /// <summary>Builds one list per supplier for every confirmed requirement due in the period.</summary>
    [HttpPost("api/order-lists/generate")]
    [HasPermission(PermissionCodes.OrderGenerate)]
    public ActionResult<GenerateOrderListsResponse> Generate(GenerateOrderListsRequest request) => NotYetBuilt();

    [HttpGet("api/order-lists")]
    [HasPermission(PermissionCodes.StockView)]
    public ActionResult<PagedResult<OrderListDto>> List([FromQuery] OrderListQuery query) => NotYetBuilt();

    [HttpGet("api/order-lists/{orderListId:guid}")]
    [HasPermission(PermissionCodes.StockView)]
    public ActionResult<OrderListDto> Get(Guid orderListId) => NotYetBuilt();

    [HttpPost("api/order-lists/{orderListId:guid}/submit")]
    [HasPermission(PermissionCodes.OrderGenerate)]
    public ActionResult<OrderListDto> Submit(Guid orderListId, OrderListActionRequest request) => NotYetBuilt();

    /// <summary>The approver must hold order.approve and must not be the user who generated the list.</summary>
    [HttpPost("api/order-lists/{orderListId:guid}/approve")]
    [HasPermission(PermissionCodes.OrderApprove)]
    public ActionResult<OrderListDto> Approve(Guid orderListId, OrderListActionRequest request) => NotYetBuilt();

    [HttpPost("api/order-lists/{orderListId:guid}/mark-placed")]
    [HasPermission(PermissionCodes.OrderPlace)]
    public ActionResult<OrderListDto> MarkPlaced(Guid orderListId, OrderListActionRequest request) => NotYetBuilt();
}
