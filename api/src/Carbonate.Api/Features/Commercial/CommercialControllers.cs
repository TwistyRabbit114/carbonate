using Carbonate.Api.Common;
using Carbonate.Api.Platform.Auth;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Platform.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Carbonate.Api.Features.Commercial;

/// <summary>Costings and their approval (FR-11, FR-14, FR-15).</summary>
[ApiController]
public class QuotesController(ICommercialService commercial) : ApiControllerBase
{
    [HttpGet("api/events/{eventId:guid}/quotes")]
    [HasPermission(PermissionCodes.QuoteView)]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> List(Guid eventId, CancellationToken ct) =>
        Ok(await commercial.ListQuotesAsync(eventId, ct));

    [HttpPost("api/events/{eventId:guid}/quotes")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    [ProducesResponseType<QuoteDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteDto>> Create(Guid eventId, SaveQuoteRequest request, CancellationToken ct)
    {
        var quote = await commercial.CreateQuoteAsync(eventId, request, ct);
        return CreatedAtAction(nameof(Get), new { quoteId = quote.QuoteId }, quote);
    }

    /// <summary>Starts a new costing from an earlier one for the same client.</summary>
    [HttpPost("api/events/{eventId:guid}/quotes/copy")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    [ProducesResponseType<QuoteDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteDto>> Copy(Guid eventId, CopyQuoteRequest request, CancellationToken ct)
    {
        var quote = await commercial.CopyQuoteAsync(eventId, request, ct);
        return CreatedAtAction(nameof(Get), new { quoteId = quote.QuoteId }, quote);
    }

    [HttpGet("api/quotes/{quoteId:guid}")]
    [HasPermission(PermissionCodes.QuoteView)]
    public async Task<ActionResult<QuoteDto>> Get(Guid quoteId, CancellationToken ct) =>
        Ok(await commercial.GetQuoteAsync(quoteId, ct));

    /// <summary>Editing an issued quote creates a new version and supersedes the old one.</summary>
    [HttpPut("api/quotes/{quoteId:guid}")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public async Task<ActionResult<QuoteDto>> Update(Guid quoteId, SaveQuoteRequest request, CancellationToken ct) =>
        Ok(await commercial.UpdateQuoteAsync(quoteId, request, ct));

    /// <summary>A quote the Director wrote is approved on submit.</summary>
    [HttpPost("api/quotes/{quoteId:guid}/submit")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public async Task<ActionResult<QuoteDto>> Submit(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        Ok(await commercial.SubmitQuoteAsync(quoteId, request, ct));

    [HttpPost("api/quotes/{quoteId:guid}/approve")]
    [HasPermission(PermissionCodes.QuoteApprove)]
    public async Task<ActionResult<QuoteDto>> Approve(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        Ok(await commercial.ApproveQuoteAsync(quoteId, request, ct));

    /// <summary>Refused with 422 while a quote above the approval threshold is not yet approved (FR-14).</summary>
    [HttpPost("api/quotes/{quoteId:guid}/issue")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public async Task<ActionResult<QuoteDto>> Issue(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        Ok(await commercial.IssueQuoteAsync(quoteId, request, ct));

    [HttpPost("api/quotes/{quoteId:guid}/accept")]
    [HasPermission(PermissionCodes.QuoteEdit)]
    public async Task<ActionResult<QuoteDto>> Accept(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        Ok(await commercial.AcceptQuoteAsync(quoteId, request, ct));
}

[ApiController]
public class ConfirmationsController(ICommercialService commercial) : ApiControllerBase
{
    /// <summary>The confirmation on the event, so an invoice can be raised against its id (FR-13).</summary>
    [HttpGet("api/events/{eventId:guid}/confirmation")]
    [HasPermission(PermissionCodes.InvoiceView)]
    public async Task<ActionResult<ConfirmationDto>> Get(Guid eventId, CancellationToken ct) =>
        Ok(await commercial.GetConfirmationAsync(eventId, ct));

    /// <summary>Records a PO or deposit and moves the event from Enquired to ConfirmedInPlanning (FR-12).</summary>
    [HttpPost("api/events/{eventId:guid}/confirmation")]
    [HasPermission(PermissionCodes.ConfirmationRecord)]
    [ProducesResponseType<ConfirmationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ConfirmationDto>> Record(Guid eventId, RecordConfirmationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await commercial.RecordConfirmationAsync(eventId, request, ct));
}

[ApiController]
public class InvoicesController(ICommercialService commercial) : ApiControllerBase
{
    [HttpGet("api/invoices")]
    [HasPermission(PermissionCodes.InvoiceView)]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> List([FromQuery] InvoiceListQuery query, CancellationToken ct) =>
        Ok(await commercial.ListInvoicesAsync(query, ct));

    /// <summary>Needs a confirmation on the event and carries its PO number or deposit reference (FR-13).</summary>
    [HttpPost("api/events/{eventId:guid}/invoices")]
    [HasPermission(PermissionCodes.InvoiceManage)]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceDto>> Create(Guid eventId, CreateInvoiceRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await commercial.CreateInvoiceAsync(eventId, request, ct));

    /// <summary>Marks an invoice paid or void.</summary>
    [HttpPatch("api/invoices/{invoiceId:guid}")]
    [HasPermission(PermissionCodes.InvoiceManage)]
    public async Task<ActionResult<InvoiceDto>> Update(Guid invoiceId, UpdateInvoiceRequest request, CancellationToken ct) =>
        Ok(await commercial.UpdateInvoiceAsync(invoiceId, request, ct));

    /// <summary>Finished events that have no invoice yet, oldest first, so none is forgotten (FR-16).</summary>
    [HttpGet("api/invoices/uninvoiced-events")]
    [HasPermission(PermissionCodes.InvoiceManage)]
    public async Task<ActionResult<IReadOnlyList<UninvoicedEventDto>>> Uninvoiced(CancellationToken ct) =>
        Ok(await commercial.ListUninvoicedEventsAsync(ct));
}

[ApiController]
public class ReconciliationController : ApiControllerBase
{
    /// <summary>Should have (FR-17). Not built yet.</summary>
    [HttpGet("api/events/{eventId:guid}/reconciliation")]
    [HasPermission(PermissionCodes.ReconciliationEdit)]
    public ActionResult<ReconciliationDto> Get(Guid eventId) => NotYetBuilt();

    [HttpPut("api/events/{eventId:guid}/reconciliation")]
    [HasPermission(PermissionCodes.ReconciliationEdit)]
    public ActionResult<ReconciliationDto> Save(Guid eventId, SaveReconciliationRequest request) => NotYetBuilt();
}
