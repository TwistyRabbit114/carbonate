using Carbonate.Application.Common;
using Carbonate.Application.Features.Commercial;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Commercial;
using Carbonate.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Commercial;

internal sealed class CommercialRepository(CemDbContext db) : ICommercialRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int InvoiceNumberAttempts = 5;
    private const int CostHistoryLimit = 20;

    public Task<EventFacts?> GetEventFactsAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct)
    {
        var events = db.Events.AsNoTracking().Where(e => e.EventId == eventId && e.IsActive);
        if (visibleToUserId is not null)
        {
            events = events.Where(e => e.CrewAssignments.Any(c => c.UserId == visibleToUserId));
        }

        return events.Select(e => new EventFacts(
                e.EventId,
                e.EventCode,
                e.ClientId,
                db.Clients.Where(c => c.ClientId == e.ClientId).Select(c => c.Name).First(),
                e.Status,
                e.PackSizeActual ?? e.PackSizeEstimated,
                db.Clients.Where(c => c.ClientId == e.ClientId).Select(c => c.PaymentTermsDays).First()))
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<Quote>> ListQuotesAsync(Guid eventId, CancellationToken ct) =>
        db.Quotes.AsNoTracking().Include(q => q.Lines).Where(q => q.EventId == eventId).ToListAsync(ct);

    public Task<Quote?> FindQuoteAsync(Guid quoteId, CancellationToken ct) =>
        db.Quotes.AsNoTracking().Include(q => q.Lines).FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);

    public Task<Quote?> FindQuoteForUpdateAsync(Guid quoteId, CancellationToken ct) =>
        db.Quotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);

    public async Task<QuoteSource?> FindQuoteSourceAsync(Guid quoteId, CancellationToken ct)
    {
        var quote = await db.Quotes.AsNoTracking().Include(q => q.Lines).FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);
        if (quote is null)
        {
            return null;
        }

        var clientId = await db.Events.Where(e => e.EventId == quote.EventId).Select(e => e.ClientId).FirstAsync(ct);
        return new QuoteSource(quote, clientId);
    }

    public async Task<int> MaxQuoteVersionAsync(Guid eventId, CancellationToken ct) =>
        await db.Quotes.Where(q => q.EventId == eventId).MaxAsync(q => (int?)q.Version, ct) ?? 0;

    public void AddQuote(Quote quote) => db.Quotes.Add(quote);

    public void RemoveQuoteLines(IEnumerable<QuoteLine> lines) => db.QuoteLines.RemoveRange(lines);

    public void AddQuoteLines(IEnumerable<QuoteLine> lines) => db.QuoteLines.AddRange(lines);

    public void ExpectRowVersion(Quote quote, byte[] rowVersion) =>
        db.Entry(quote).Property(q => q.RowVersion).OriginalValue = rowVersion;

    public void Touch(Quote quote) => db.Entry(quote).Property(q => q.Version).IsModified = true;

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
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two people added a costing to the same event at the same moment and took the same version.
            throw ProblemException.Conflict(
                "/problems/concurrency-conflict",
                "Another costing was just added.",
                "Reload the event's costings and try again.");
        }
    }

    public Task<bool> DocumentBelongsToEventAsync(Guid documentId, Guid eventId, CancellationToken ct) =>
        db.Documents.AnyAsync(d => d.DocumentId == documentId && d.EventId == eventId, ct);

    public void AddConfirmation(EventConfirmation confirmation) => db.EventConfirmations.Add(confirmation);

    public Task<EventConfirmation?> FindConfirmationForEventAsync(Guid eventId, CancellationToken ct) =>
        db.EventConfirmations.AsNoTracking().Where(c => c.EventId == eventId)
            .OrderByDescending(c => c.ConfirmedAt).FirstOrDefaultAsync(ct);

    public Task<EventConfirmation?> FindConfirmationAsync(Guid confirmationId, CancellationToken ct) =>
        db.EventConfirmations.AsNoTracking().FirstOrDefaultAsync(c => c.ConfirmationId == confirmationId, ct);

    public Task<decimal?> LatestAcceptedQuoteTotalAsync(Guid eventId, CancellationToken ct) =>
        db.Quotes.AsNoTracking()
            .Where(q => q.EventId == eventId && q.Status == QuoteStatus.Accepted)
            .OrderByDescending(q => q.Version)
            .Select(q => (decimal?)q.TotalIncVat)
            .FirstOrDefaultAsync(ct);

    public async Task AddInvoiceAsync(Invoice invoice, int year, CancellationToken ct)
    {
        var prefix = $"INV-{year}-";

        for (var attempt = 1; ; attempt++)
        {
            var existing = await db.Invoices.AsNoTracking()
                .Where(i => i.InvoiceNumber.StartsWith(prefix))
                .Select(i => i.InvoiceNumber)
                .ToListAsync(ct);
            var highest = existing
                .Select(n => int.TryParse(n[prefix.Length..], out var number) ? number : 0)
                .DefaultIfEmpty(0)
                .Max();

            invoice.InvoiceNumber = $"{prefix}{highest + 1:0000}";
            db.Invoices.Add(invoice);

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < InvoiceNumberAttempts)
            {
                // Someone else took this number a moment ago. Take the next one.
                db.Entry(invoice).State = EntityState.Detached;
            }
        }
    }

    public Task<Invoice?> FindInvoiceAsync(Guid invoiceId, CancellationToken ct) =>
        db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

    public Task<InvoiceDto?> GetInvoiceAsync(Guid invoiceId, CancellationToken ct) =>
        InvoiceQuery().Where(i => i.InvoiceId == invoiceId).FirstOrDefaultAsync(ct);

    public async Task<PagedResult<InvoiceDto>> ListInvoicesAsync(InvoiceListQuery query, CancellationToken ct)
    {
        var invoices = InvoiceQuery();
        if (query.Status is { } status)
        {
            invoices = invoices.Where(i => i.Status == status);
        }

        var total = await invoices.CountAsync(ct);
        var items = await invoices
            .OrderByDescending(i => i.IssuedDate).ThenByDescending(i => i.InvoiceNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<InvoiceDto> { Items = items, Page = query.Page, PageSize = query.PageSize, Total = total };
    }

    public async Task<IReadOnlyList<CostHistoryRow>> CostHistoryAsync(Guid eventId, Guid clientId, CancellationToken ct)
    {
        var events = await db.Events.AsNoTracking()
            .Where(e => e.ClientId == clientId && e.EventId != eventId && e.IsActive)
            .OrderByDescending(e => e.EventDate)
            .Take(CostHistoryLimit)
            .Select(e => new { e.EventId, e.EventCode, e.Name, e.EventDate, PackSize = e.PackSizeActual ?? e.PackSizeEstimated })
            .ToListAsync(ct);

        var ids = events.Select(e => e.EventId).ToList();
        var quotes = await db.Quotes.AsNoTracking().Include(q => q.Lines)
            .Where(q => ids.Contains(q.EventId) && q.Status != QuoteStatus.Superseded)
            .ToListAsync(ct);

        // The costing that was actually accepted if there is one, otherwise the latest one.
        return
        [
            .. events.Select(e =>
            {
                var chosen = quotes.Where(q => q.EventId == e.EventId)
                    .OrderByDescending(q => q.Status == QuoteStatus.Accepted).ThenByDescending(q => q.Version)
                    .FirstOrDefault();

                return new CostHistoryRow(
                    e.EventId, e.EventCode, e.Name, e.EventDate, e.PackSize,
                    chosen?.SubtotalExVat,
                    chosen?.TotalIncVat,
                    chosen?.Lines.Sum(l => Math.Round(l.Quantity * l.UnitCostToUs, 2, MidpointRounding.AwayFromZero)));
            }),
        ];
    }

    private IQueryable<InvoiceDto> InvoiceQuery() =>
        from i in db.Invoices.AsNoTracking()
        join e in db.Events on i.EventId equals e.EventId
        join c in db.EventConfirmations on i.ConfirmationId equals c.ConfirmationId
        select new InvoiceDto
        {
            InvoiceId = i.InvoiceId,
            EventId = i.EventId,
            EventCode = e.EventCode,
            ConfirmationId = i.ConfirmationId,
            ConfirmationReference = c.ClientPoNumber ?? c.DepositReference ?? "",
            InvoiceNumber = i.InvoiceNumber,
            IssuedDate = i.IssuedDate,
            DueDate = i.DueDate,
            AmountIncVat = i.AmountIncVat,
            Status = i.Status,
            PaidDate = i.PaidDate,
        };

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
