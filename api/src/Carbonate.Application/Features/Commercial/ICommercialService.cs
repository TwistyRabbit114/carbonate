using Carbonate.Application.Common;
using Carbonate.Application.Features.Events;

namespace Carbonate.Application.Features.Commercial;

/// <summary>
/// Costings, confirmation and invoices (FR-09, FR-11 to FR-15). Every method checks the caller's
/// permission itself, and an event the caller may not see is a 404.
/// </summary>
public interface ICommercialService
{
    Task<IReadOnlyList<QuoteDto>> ListQuotesAsync(Guid eventId, CancellationToken ct);
    Task<QuoteDto> GetQuoteAsync(Guid quoteId, CancellationToken ct);
    Task<QuoteDto> CreateQuoteAsync(Guid eventId, SaveQuoteRequest request, CancellationToken ct);
    Task<QuoteDto> CopyQuoteAsync(Guid eventId, CopyQuoteRequest request, CancellationToken ct);
    Task<QuoteDto> UpdateQuoteAsync(Guid quoteId, SaveQuoteRequest request, CancellationToken ct);
    Task<QuoteDto> SubmitQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct);
    Task<QuoteDto> ApproveQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct);
    Task<QuoteDto> IssueQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct);
    Task<QuoteDto> AcceptQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct);

    Task<ConfirmationDto> GetConfirmationAsync(Guid eventId, CancellationToken ct);
    Task<ConfirmationDto> RecordConfirmationAsync(Guid eventId, RecordConfirmationRequest request, CancellationToken ct);

    Task<PagedResult<InvoiceDto>> ListInvoicesAsync(InvoiceListQuery query, CancellationToken ct);
    Task<InvoiceDto> CreateInvoiceAsync(Guid eventId, CreateInvoiceRequest request, CancellationToken ct);
    Task<InvoiceDto> UpdateInvoiceAsync(Guid invoiceId, UpdateInvoiceRequest request, CancellationToken ct);

    Task<IReadOnlyList<CostHistoryItem>> GetCostHistoryAsync(Guid eventId, CancellationToken ct);
}
