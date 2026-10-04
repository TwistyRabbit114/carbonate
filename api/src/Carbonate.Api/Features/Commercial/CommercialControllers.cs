using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Commercial;

/// <summary>Costings and their approval (FR-11, FR-14, FR-15). Stubs until the module lands; C owns the bodies.</summary>
[ApiController]
public class QuotesController : ApiControllerBase
{
    [HttpGet("api/events/{eventId:guid}/quotes")]
    [HasPermission(PermissionCodes.QuoteView)]
    public ActionResult<IReadOnlyList<QuoteDto>> List(Guid eventId) => NotYetBuilt();

    [HttpPost("api/events/{eventId:guid}/quotes")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    [ProducesResponseType<QuoteDto>(StatusCodes.Status201Created)]
    public ActionResult<QuoteDto> Create(Guid eventId, SaveQuoteRequest request) => NotYetBuilt();

    /// <summary>Starts a new costing from an earlier one for the same client.</summary>
    [HttpPost("api/events/{eventId:guid}/quotes/copy")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    [ProducesResponseType<QuoteDto>(StatusCodes.Status201Created)]
    public ActionResult<QuoteDto> Copy(Guid eventId, CopyQuoteRequest request) => NotYetBuilt();

    [HttpGet("api/quotes/{quoteId:guid}")]
    [HasPermission(PermissionCodes.QuoteView)]
    public ActionResult<QuoteDto> Get(Guid quoteId) => NotYetBuilt();

    /// <summary>Editing an issued quote creates a new version and supersedes the old one.</summary>
    [HttpPut("api/quotes/{quoteId:guid}")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public ActionResult<QuoteDto> Update(Guid quoteId, SaveQuoteRequest request) => NotYetBuilt();

    /// <summary>A quote the Director wrote is approved on submit.</summary>
    [HttpPost("api/quotes/{quoteId:guid}/submit")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public ActionResult<QuoteDto> Submit(Guid quoteId, QuoteActionRequest request) => NotYetBuilt();

    [HttpPost("api/quotes/{quoteId:guid}/approve")]
    [HasPermission(PermissionCodes.QuoteApprove)]
    public ActionResult<QuoteDto> Approve(Guid quoteId, QuoteActionRequest request) => NotYetBuilt();

    /// <summary>Refused with 422 while a quote above the approval threshold is not yet approved (FR-14).</summary>
    [HttpPost("api/quotes/{quoteId:guid}/issue")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public ActionResult<QuoteDto> Issue(Guid quoteId, QuoteActionRequest request) => NotYetBuilt();

    [HttpPost("api/quotes/{quoteId:guid}/accept")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public ActionResult<QuoteDto> Accept(Guid quoteId, QuoteActionRequest request) => NotYetBuilt();
}

[ApiController]
public class ConfirmationsController : ApiControllerBase
{
    /// <summary>Records a PO or deposit and moves the event from Enquired to ConfirmedInPlanning (FR-12).</summary>
    [HttpPost("api/events/{eventId:guid}/confirmation")]
    [HasPermission(PermissionCodes.ConfirmationRecord)]
    [ProducesResponseType<ConfirmationDto>(StatusCodes.Status201Created)]
    public ActionResult<ConfirmationDto> Record(Guid eventId, RecordConfirmationRequest request) => NotYetBuilt();
}

[ApiController]
public class InvoicesController : ApiControllerBase
{
    [HttpGet("api/invoices")]
    [HasPermission(PermissionCodes.InvoiceView)]
    public ActionResult<PagedResult<InvoiceDto>> List([FromQuery] InvoiceListQuery query) => NotYetBuilt();

    /// <summary>Needs a confirmation on the event and carries its PO number or deposit reference (FR-13).</summary>
    [HttpPost("api/events/{eventId:guid}/invoices")]
    [HasPermission(PermissionCodes.InvoiceManage)]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status201Created)]
    public ActionResult<InvoiceDto> Create(Guid eventId, CreateInvoiceRequest request) => NotYetBuilt();

    /// <summary>Marks an invoice issued or paid.</summary>
    [HttpPatch("api/invoices/{invoiceId:guid}")]
    [HasPermission(PermissionCodes.InvoiceManage)]
    public ActionResult<InvoiceDto> Update(Guid invoiceId, UpdateInvoiceRequest request) => NotYetBuilt();

    /// <summary>Should have (FR-16).</summary>
    [HttpGet("api/invoices/uninvoiced-events")]
    [HasPermission(PermissionCodes.InvoiceManage)]
    public ActionResult<IReadOnlyList<UninvoicedEventDto>> Uninvoiced() => NotYetBuilt();
}

[ApiController]
public class ReconciliationController : ApiControllerBase
{
    /// <summary>Should have (FR-17).</summary>
    [HttpGet("api/events/{eventId:guid}/reconciliation")]
    [HasPermission(PermissionCodes.ReconciliationEdit)]
    public ActionResult<ReconciliationDto> Get(Guid eventId) => NotYetBuilt();

    [HttpPut("api/events/{eventId:guid}/reconciliation")]
    [HasPermission(PermissionCodes.ReconciliationEdit)]
    public ActionResult<ReconciliationDto> Save(Guid eventId, SaveReconciliationRequest request) => NotYetBuilt();
}
