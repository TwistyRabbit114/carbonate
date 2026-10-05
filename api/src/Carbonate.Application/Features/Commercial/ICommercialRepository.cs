using Carbonate.Application.Common;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Commercial;

namespace Carbonate.Application.Features.Commercial;

/// <summary>What the commercial rules need to know about an event, without loading the whole event.</summary>
public sealed record EventFacts(
    Guid EventId,
    string EventCode,
    Guid ClientId,
    string ClientName,
    EventStatus Status,
    int PackSize,
    int PaymentTermsDays);

public sealed record QuoteSource(Quote Quote, Guid ClientId);

/// <summary>One earlier event for the same client with its costing, before margin is worked out.</summary>
public sealed record CostHistoryRow(
    Guid EventId,
    string EventCode,
    string Name,
    DateOnly EventDate,
    int PackSize,
    decimal? SubtotalExVat,
    decimal? TotalIncVat,
    decimal? InternalCostTotal);

public interface ICommercialRepository
{
    /// <summary>Null if the event does not exist, is retired, or is not visible to this caller.</summary>
    Task<EventFacts?> GetEventFactsAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct);

    Task<List<Quote>> ListQuotesAsync(Guid eventId, CancellationToken ct);

    /// <summary>The quote with its lines, read-only.</summary>
    Task<Quote?> FindQuoteAsync(Guid quoteId, CancellationToken ct);

    /// <summary>The quote with its lines, tracked so it can be changed. Also carries its event id for the visibility check.</summary>
    Task<Quote?> FindQuoteForUpdateAsync(Guid quoteId, CancellationToken ct);

    Task<QuoteSource?> FindQuoteSourceAsync(Guid quoteId, CancellationToken ct);

    Task<int> MaxQuoteVersionAsync(Guid eventId, CancellationToken ct);

    void AddQuote(Quote quote);
    void RemoveQuoteLines(IEnumerable<QuoteLine> lines);

    /// <summary>
    /// Adds lines to a quote that is already tracked. They must be added explicitly: keys are generated in
    /// code, so EF would otherwise take lines added through the collection for rows that already exist.
    /// </summary>
    void AddQuoteLines(IEnumerable<QuoteLine> lines);
    void ExpectRowVersion(Quote quote, byte[] rowVersion);

    /// <summary>Forces the quote row to update so its row version moves on when only its lines changed.</summary>
    void Touch(Quote quote);

    /// <exception cref="ConcurrencyConflictException">Someone else saved first.</exception>
    Task SaveChangesAsync(CancellationToken ct);

    Task<bool> DocumentBelongsToEventAsync(Guid documentId, Guid eventId, CancellationToken ct);
    void AddConfirmation(EventConfirmation confirmation);
    /// <summary>The most recent confirmation on the event, or null if it has none.</summary>
    Task<EventConfirmation?> FindConfirmationForEventAsync(Guid eventId, CancellationToken ct);

    Task<EventConfirmation?> FindConfirmationAsync(Guid confirmationId, CancellationToken ct);

    Task<decimal?> LatestAcceptedQuoteTotalAsync(Guid eventId, CancellationToken ct);

    /// <summary>
    /// Allocates the next invoice number for the year, adds the invoice and saves it. If another request
    /// takes the same number first, it takes the next one, so numbers are unique and never reused.
    /// </summary>
    Task AddInvoiceAsync(Invoice invoice, int year, CancellationToken ct);

    Task<Invoice?> FindInvoiceAsync(Guid invoiceId, CancellationToken ct);
    Task<InvoiceDto?> GetInvoiceAsync(Guid invoiceId, CancellationToken ct);
    /// <summary>Finished events with no invoice that is not void, oldest first.</summary>
    Task<IReadOnlyList<UninvoicedEventDto>> ListUninvoicedEventsAsync(Guid? visibleToUserId, DateOnly today, CancellationToken ct);

    Task<PagedResult<InvoiceDto>> ListInvoicesAsync(InvoiceListQuery query, CancellationToken ct);

    Task<IReadOnlyList<CostHistoryRow>> CostHistoryAsync(Guid eventId, Guid clientId, CancellationToken ct);
}
