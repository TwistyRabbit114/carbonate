using Carbonate.Application.Common;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Features.Lifecycle;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Commercial;
using Microsoft.Extensions.Options;

namespace Carbonate.Application.Features.Commercial;

public sealed class CommercialService(
    ICommercialRepository repository,
    IEventLifecycleService lifecycle,
    ITransactionRunner transactions,
    IAuditService audit,
    IFinancialMasker masker,
    ICurrentUser user,
    IOptions<FinanceOptions> finance,
    IOptions<QuoteOptions> quoteOptions,
    TimeProvider clock) : ICommercialService
{
    private const string EventNotFound = "That event was not found.";
    private const string QuoteNotFound = "That costing was not found.";

    private Guid? VisibleTo => user.HasPermission(PermissionCodes.EventViewAll) ? null : user.UserId;

    // ---- quotes ---------------------------------------------------------------------------------

    public async Task<IReadOnlyList<QuoteDto>> ListQuotesAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.QuoteView);
        var facts = await FactsAsync(eventId, ct);

        var quotes = await repository.ListQuotesAsync(eventId, ct);
        var dtos = quotes.OrderByDescending(q => q.Version).Select(q => ToDto(q, facts)).ToList();
        masker.Mask(dtos, user);
        return dtos;
    }

    public async Task<QuoteDto> GetQuoteAsync(Guid quoteId, CancellationToken ct)
    {
        Require(PermissionCodes.QuoteView);

        var quote = await repository.FindQuoteAsync(quoteId, ct) ?? throw ProblemException.NotFound(QuoteNotFound);
        return Masked(ToDto(quote, await FactsAsync(quote.EventId, ct)));
    }

    public async Task<QuoteDto> CreateQuoteAsync(Guid eventId, SaveQuoteRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.QuoteEdit);
        var facts = await FactsAsync(eventId, ct);

        var quote = NewQuote(eventId, await repository.MaxQuoteVersionAsync(eventId, ct) + 1, request.ValidUntil, request.Lines, null);
        repository.AddQuote(quote);
        await repository.SaveChangesAsync(ct);
        await audit.RecordAsync("quote.create", nameof(Quote), quote.QuoteId.ToString(), null, Snapshot(quote), user.UserId, ct);

        return await ReloadAsync(quote.QuoteId, facts, ct);
    }

    public async Task<QuoteDto> CopyQuoteAsync(Guid eventId, CopyQuoteRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.QuoteEdit);
        var facts = await FactsAsync(eventId, ct);

        var source = await repository.FindQuoteSourceAsync(request.SourceQuoteId, ct)
            ?? throw ProblemException.NotFound(QuoteNotFound);

        // Prices are the client's, so a costing is only copied between events for the same client (FR-15).
        if (source.ClientId != facts.ClientId)
        {
            throw ProblemException.BusinessRule("You can only copy a costing from an earlier event for the same client.");
        }

        var lines = source.Quote.Lines.Select(l => new SaveQuoteLine
        {
            Description = l.Description,
            Quantity = l.Quantity,
            Category = l.Category,
            UnitCostToUs = l.UnitCostToUs,
            UnitPriceToClient = l.UnitPriceToClient,
        }).ToList();

        // Totals are worked out again, so a copy always uses today's VAT rate.
        var quote = NewQuote(eventId, await repository.MaxQuoteVersionAsync(eventId, ct) + 1, null, lines, source.Quote.QuoteId);
        repository.AddQuote(quote);
        await repository.SaveChangesAsync(ct);
        await audit.RecordAsync("quote.copy", nameof(Quote), quote.QuoteId.ToString(), null,
            new { CopiedFrom = source.Quote.QuoteId, Snapshot = Snapshot(quote) }, user.UserId, ct);

        return await ReloadAsync(quote.QuoteId, facts, ct);
    }

    public async Task<QuoteDto> UpdateQuoteAsync(Guid quoteId, SaveQuoteRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.QuoteEdit);

        var quote = await repository.FindQuoteForUpdateAsync(quoteId, ct) ?? throw ProblemException.NotFound(QuoteNotFound);
        var facts = await FactsAsync(quote.EventId, ct);

        var outcome = QuoteRules.OnEdit(quote.Status);
        if (outcome == EditOutcome.Refused)
        {
            throw ProblemException.BusinessRule("A costing that has been accepted or replaced cannot be changed. Start a new one.");
        }

        repository.ExpectRowVersion(quote, RowVersions.Decode(request.RowVersion));
        var before = Snapshot(quote);
        var totals = Calculate(request.Lines);

        if (outcome == EditOutcome.NewVersion)
        {
            // An issued quote's totals never change: the old version is kept and a new one takes over.
            quote.Status = QuoteStatus.Superseded;
            repository.Touch(quote);
            var next = NewQuote(quote.EventId, await repository.MaxQuoteVersionAsync(quote.EventId, ct) + 1, request.ValidUntil, request.Lines, null);
            repository.AddQuote(next);
            await SaveAsync(quote.QuoteId, facts, ct);
            await audit.RecordAsync("quote.new_version", nameof(Quote), next.QuoteId.ToString(), before, Snapshot(next), user.UserId, ct);
            return await ReloadAsync(next.QuoteId, facts, ct);
        }

        repository.RemoveQuoteLines([.. quote.Lines]);
        quote.Lines.Clear();
        var newLines = BuildLines(request.Lines, totals);
        foreach (var line in newLines)
        {
            line.QuoteId = quote.QuoteId;
        }

        repository.AddQuoteLines(newLines);

        ApplyTotals(quote, totals);
        quote.ValidUntil = request.ValidUntil;
        if (outcome == EditOutcome.InPlaceAndReopen)
        {
            // The numbers changed after approval, so the approval no longer covers them.
            quote.Status = QuoteStatus.Draft;
            quote.ApprovedByUserId = null;
            quote.ApprovedAt = null;
        }

        repository.Touch(quote);
        await SaveAsync(quoteId, facts, ct);
        await audit.RecordAsync("quote.update", nameof(Quote), quoteId.ToString(), before, Snapshot(quote), user.UserId, ct);

        return await ReloadAsync(quoteId, facts, ct);
    }

    public Task<QuoteDto> SubmitQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        ActAsync(quoteId, request, PermissionCodes.QuoteEdit, "quote.submit", (quote, now) =>
        {
            if (!QuoteRules.CanSubmit(quote.Status))
            {
                throw InvalidTransition("Only a draft costing can be submitted for approval.");
            }

            RequireLines(quote);

            // A costing the Director wrote counts as approved as soon as it is submitted.
            if (user.HasPermission(PermissionCodes.QuoteApprove))
            {
                Approve(quote, now);
            }
            else
            {
                quote.Status = QuoteStatus.PendingApproval;
            }
        }, ct);

    public Task<QuoteDto> ApproveQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        ActAsync(quoteId, request, PermissionCodes.QuoteApprove, "quote.approve", (quote, now) =>
        {
            if (!QuoteRules.CanApprove(quote.Status))
            {
                throw InvalidTransition("Only a costing that is waiting for approval can be approved.");
            }

            Approve(quote, now);
        }, ct);

    public Task<QuoteDto> IssueQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        ActAsync(quoteId, request, PermissionCodes.QuoteEdit, "quote.issue", (quote, now) =>
        {
            if (!QuoteRules.CanIssue(quote.Status))
            {
                throw InvalidTransition("This costing has already been issued or replaced.");
            }

            RequireLines(quote);

            if (QuoteRules.RequiresApproval(quote.TotalIncVat, quoteOptions.Value.ApprovalThresholdZar, quote.ApprovedByUserId))
            {
                throw ProblemException.BusinessRule("This costing is above the approval limit, so the Director must approve it before it is issued.");
            }

            quote.Status = QuoteStatus.Issued;
            quote.IssuedAt = now;
        }, ct);

    public Task<QuoteDto> AcceptQuoteAsync(Guid quoteId, QuoteActionRequest request, CancellationToken ct) =>
        ActAsync(quoteId, request, PermissionCodes.QuoteEdit, "quote.accept", (quote, now) =>
        {
            if (!QuoteRules.CanAccept(quote.Status))
            {
                throw InvalidTransition("Only an issued costing can be accepted.");
            }

            quote.Status = QuoteStatus.Accepted;
            quote.AcceptedAt = now;
        }, ct);

    // ---- confirmation ---------------------------------------------------------------------------

    public async Task<ConfirmationDto> RecordConfirmationAsync(Guid eventId, RecordConfirmationRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.ConfirmationRecord);
        var facts = await FactsAsync(eventId, ct);

        if (facts.Status != EventStatus.Enquired)
        {
            throw ProblemException.BusinessRule("That event has already been confirmed, or is closed.");
        }

        if (request.DocumentId is { } documentId && !await repository.DocumentBelongsToEventAsync(documentId, eventId, ct))
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                ["documentId"] = ["That document does not belong to this event."],
            });
        }

        var confirmation = new EventConfirmation
        {
            EventId = eventId,
            DocumentId = request.DocumentId,
            ConfirmationType = request.ConfirmationType,
            ClientPoNumber = request.ClientPoNumber,
            PoReceivedDate = request.PoReceivedDate,
            PoAmount = request.PoAmount,
            DepositAmount = request.DepositAmount,
            DepositPaidDate = request.DepositPaidDate,
            DepositReference = request.DepositReference,
            ConfirmedAt = clock.GetUtcNow().UtcDateTime,
        };

        // The confirmation and the move to ConfirmedInPlanning stand or fall together.
        await transactions.RunAsync(async token =>
        {
            repository.AddConfirmation(confirmation);
            await repository.SaveChangesAsync(token);

            await lifecycle.ConfirmAsync(eventId, user.UserId, token);
            await repository.SaveChangesAsync(token);

            await audit.RecordAsync("event.confirm", "Event", eventId.ToString(), null,
                new { confirmation.ConfirmationId, confirmation.ConfirmationType, confirmation.ClientPoNumber, confirmation.DepositReference },
                user.UserId, token);
            return confirmation.ConfirmationId;
        }, ct);

        return Masked(ToDto(confirmation));
    }

    // ---- invoices -------------------------------------------------------------------------------

    public async Task<PagedResult<InvoiceDto>> ListInvoicesAsync(InvoiceListQuery query, CancellationToken ct)
    {
        Require(PermissionCodes.InvoiceView);

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);

        var page = await repository.ListInvoicesAsync(query, ct);
        masker.Mask(page, user);
        return page;
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(Guid eventId, CreateInvoiceRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.InvoiceManage);
        var facts = await FactsAsync(eventId, ct);

        var confirmation = await repository.FindConfirmationAsync(request.ConfirmationId, ct);
        if (confirmation is null || confirmation.EventId != eventId)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                ["confirmationId"] = ["An invoice needs the event's PO or deposit confirmation."],
            });
        }

        // The amount defaults to the accepted costing, so it is never retyped from memory.
        var amount = request.AmountIncVat ?? await repository.LatestAcceptedQuoteTotalAsync(eventId, ct)
            ?? throw ProblemException.BusinessRule("There is no accepted costing for this event, so enter the invoice amount.");

        var issued = request.IssuedDate;
        var invoice = new Invoice
        {
            EventId = eventId,
            ConfirmationId = confirmation.ConfirmationId,
            IssuedDate = issued,
            DueDate = issued.AddDays(facts.PaymentTermsDays),
            AmountIncVat = amount,
            Status = InvoiceStatus.Issued,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };

        await repository.AddInvoiceAsync(invoice, issued.Year, ct);
        await audit.RecordAsync("invoice.create", nameof(Invoice), invoice.InvoiceId.ToString(), null,
            new { invoice.InvoiceNumber, invoice.EventId, invoice.AmountIncVat, invoice.DueDate }, user.UserId, ct);

        return await InvoiceAsync(invoice.InvoiceId, ct);
    }

    public async Task<InvoiceDto> UpdateInvoiceAsync(Guid invoiceId, UpdateInvoiceRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.InvoiceManage);

        var invoice = await repository.FindInvoiceAsync(invoiceId, ct) ?? throw ProblemException.NotFound("That invoice was not found.");
        var before = new { invoice.Status, invoice.PaidDate };

        switch (request.Status)
        {
            case InvoiceStatus.Paid when invoice.Status == InvoiceStatus.Issued:
                if (request.PaidDate is not { } paidDate)
                {
                    throw ProblemException.Validation(new Dictionary<string, string[]> { ["paidDate"] = ["Enter the date it was paid."] });
                }

                if (paidDate < invoice.IssuedDate)
                {
                    throw ProblemException.Validation(new Dictionary<string, string[]> { ["paidDate"] = ["It cannot be paid before it was issued."] });
                }

                invoice.Status = InvoiceStatus.Paid;
                invoice.PaidDate = paidDate;
                break;

            case InvoiceStatus.Void when invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Issued:
                invoice.Status = InvoiceStatus.Void;
                break;

            case InvoiceStatus.Issued when invoice.Status == InvoiceStatus.Draft:
                invoice.Status = InvoiceStatus.Issued;
                break;

            default:
                throw ProblemException.BusinessRule($"A {invoice.Status.ToString().ToLowerInvariant()} invoice cannot be marked {request.Status.ToString().ToLowerInvariant()}.");
        }

        await repository.SaveChangesAsync(ct);
        await audit.RecordAsync("invoice.update", nameof(Invoice), invoiceId.ToString(), before,
            new { invoice.Status, invoice.PaidDate }, user.UserId, ct);

        return await InvoiceAsync(invoiceId, ct);
    }

    // ---- cost history ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<CostHistoryItem>> GetCostHistoryAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.FinanceViewClientPrice);
        var facts = await FactsAsync(eventId, ct);

        // Cost and margin are only worked out for someone who may see them.
        var canSeeCost = user.HasPermission(PermissionCodes.FinanceViewInternalCost);
        var canSeeMargin = canSeeCost && user.HasPermission(PermissionCodes.FinanceViewMargin);

        var items = (await repository.CostHistoryAsync(eventId, facts.ClientId, ct)).Select(row => new CostHistoryItem
        {
            EventId = row.EventId,
            EventCode = row.EventCode,
            Name = row.Name,
            EventDate = row.EventDate,
            PackSize = row.PackSize,
            TotalIncVat = row.TotalIncVat,
            InternalCostTotal = canSeeCost ? row.InternalCostTotal is { } cost ? Math.Round(cost, 2, MidpointRounding.AwayFromZero) : null : null,
            MarginPercent = canSeeMargin ? MarginOf(row.SubtotalExVat, row.InternalCostTotal) : null,
        }).ToList();

        masker.Mask(items, user);
        return items;
    }

    // ---- helpers --------------------------------------------------------------------------------

    private void Require(string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }

    private async Task<EventFacts> FactsAsync(Guid eventId, CancellationToken ct) =>
        await repository.GetEventFactsAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(EventNotFound);

    private T Masked<T>(T dto)
    {
        masker.Mask(dto, user);
        return dto;
    }

    private async Task<QuoteDto> ReloadAsync(Guid quoteId, EventFacts facts, CancellationToken ct)
    {
        var quote = await repository.FindQuoteAsync(quoteId, ct) ?? throw ProblemException.NotFound(QuoteNotFound);
        return Masked(ToDto(quote, facts));
    }

    private async Task<InvoiceDto> InvoiceAsync(Guid invoiceId, CancellationToken ct) =>
        Masked(await repository.GetInvoiceAsync(invoiceId, ct) ?? throw ProblemException.NotFound("That invoice was not found."));

    /// <summary>The shared shape of submit, approve, issue and accept: check, change, save, audit.</summary>
    private async Task<QuoteDto> ActAsync(
        Guid quoteId, QuoteActionRequest request, string permission, string auditAction,
        Action<Quote, DateTime> change, CancellationToken ct)
    {
        Require(permission);

        var quote = await repository.FindQuoteForUpdateAsync(quoteId, ct) ?? throw ProblemException.NotFound(QuoteNotFound);
        var facts = await FactsAsync(quote.EventId, ct);

        repository.ExpectRowVersion(quote, RowVersions.Decode(request.RowVersion));
        var before = Snapshot(quote);

        change(quote, clock.GetUtcNow().UtcDateTime);
        repository.Touch(quote);

        await SaveAsync(quoteId, facts, ct);
        await audit.RecordAsync(auditAction, nameof(Quote), quoteId.ToString(), before, Snapshot(quote), user.UserId, ct);

        return await ReloadAsync(quoteId, facts, ct);
    }

    private void Approve(Quote quote, DateTime now)
    {
        quote.Status = QuoteStatus.Approved;
        quote.ApprovedByUserId = user.UserId;
        quote.ApprovedAt = now;
    }

    private static void RequireLines(Quote quote)
    {
        if (quote.Lines.Count == 0)
        {
            throw ProblemException.BusinessRule("Add at least one line to the costing first.");
        }
    }

    private static ProblemException InvalidTransition(string detail) =>
        ProblemException.Conflict("/problems/invalid-transition", "Not a valid change.", detail);

    /// <summary>Saves, and turns a lost race into a 409 carrying the current quote, masked for this caller.</summary>
    private async Task SaveAsync(Guid quoteId, EventFacts facts, CancellationToken ct)
    {
        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            var current = await repository.FindQuoteAsync(quoteId, ct);
            var dto = current is null ? null : Masked(ToDto(current, facts));
            throw ProblemException.Conflict(
                "/problems/concurrency-conflict",
                "Someone else changed this costing.",
                "Reload the costing to see their changes, then make yours again.",
                new Dictionary<string, object?> { ["current"] = dto });
        }
    }

    private QuoteTotals Calculate(IReadOnlyList<SaveQuoteLine> lines) =>
        QuoteCalculator.Calculate(
            [.. lines.Select(l => new QuoteLineInput(l.Quantity, l.UnitCostToUs ?? 0m, l.UnitPriceToClient ?? 0m))],
            finance.Value.VatRate);

    private Quote NewQuote(Guid eventId, int version, DateOnly? validUntil, IReadOnlyList<SaveQuoteLine> lines, Guid? copiedFrom)
    {
        var totals = Calculate(lines);
        var quote = new Quote
        {
            EventId = eventId,
            Version = version,
            Status = QuoteStatus.Draft,
            ValidUntil = validUntil,
            CopiedFromQuoteId = copiedFrom,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        ApplyTotals(quote, totals);
        quote.Lines = BuildLines(lines, totals);
        return quote;
    }

    private static void ApplyTotals(Quote quote, QuoteTotals totals)
    {
        quote.SubtotalExVat = totals.SubtotalExVat;
        quote.VatAmount = totals.VatAmount;
        quote.TotalIncVat = totals.TotalIncVat;
    }

    private static List<QuoteLine> BuildLines(IReadOnlyList<SaveQuoteLine> lines, QuoteTotals totals) =>
    [
        .. lines.Select((l, i) => new QuoteLine
        {
            Description = l.Description,
            Quantity = l.Quantity,
            Category = l.Category,
            UnitCostToUs = l.UnitCostToUs ?? 0m,
            UnitPriceToClient = l.UnitPriceToClient ?? 0m,
            LineTotal = totals.LineTotals[i],
        }),
    ];

    private QuoteDto ToDto(Quote quote, EventFacts facts)
    {
        var dto = new QuoteDto
        {
            QuoteId = quote.QuoteId,
            EventId = quote.EventId,
            CopiedFromQuoteId = quote.CopiedFromQuoteId,
            Version = quote.Version,
            Status = quote.Status,
            SubtotalExVat = quote.SubtotalExVat,
            VatAmount = quote.VatAmount,
            TotalIncVat = quote.TotalIncVat,
            RequiresApproval = quote.Status is QuoteStatus.Draft or QuoteStatus.PendingApproval or QuoteStatus.Approved
                && QuoteRules.RequiresApproval(quote.TotalIncVat, quoteOptions.Value.ApprovalThresholdZar, quote.ApprovedByUserId),
            ValidUntil = quote.ValidUntil,
            IssuedAt = quote.IssuedAt,
            AcceptedAt = quote.AcceptedAt,
            ApprovedByUserId = quote.ApprovedByUserId,
            ApprovedAt = quote.ApprovedAt,
            CreatedAt = quote.CreatedAt,
            RowVersion = RowVersions.Encode(quote.RowVersion),
            Lines =
            [
                .. quote.Lines.OrderBy(l => l.Description).Select(l => new QuoteLineDto
                {
                    QuoteLineId = l.QuoteLineId,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Category = l.Category,
                    UnitCostToUs = l.UnitCostToUs,
                    UnitPriceToClient = l.UnitPriceToClient,
                    LineTotal = l.LineTotal,
                }),
            ],
        };

        // Cost and margin are only worked out for someone who may see them.
        if (user.HasPermission(PermissionCodes.FinanceViewInternalCost))
        {
            dto.InternalCostTotal = quote.Lines.Sum(l => Math.Round(l.Quantity * l.UnitCostToUs, 2, MidpointRounding.AwayFromZero));
            if (user.HasPermission(PermissionCodes.FinanceViewMargin))
            {
                dto.MarginPercent = MarginOf(quote.SubtotalExVat, dto.InternalCostTotal);
                if (MarginBands.For(facts.PackSize, quoteOptions.Value.MarginBands) is { } band)
                {
                    dto.TargetMarginBand = new MarginBandDto { TargetMinPct = band.TargetMinPct, TargetMaxPct = band.TargetMaxPct };
                }
            }
        }

        return dto;
    }

    private static ConfirmationDto ToDto(EventConfirmation c) => new()
    {
        ConfirmationId = c.ConfirmationId,
        EventId = c.EventId,
        ConfirmationType = c.ConfirmationType,
        ClientPoNumber = c.ClientPoNumber,
        PoReceivedDate = c.PoReceivedDate,
        PoAmount = c.PoAmount,
        DepositAmount = c.DepositAmount,
        DepositPaidDate = c.DepositPaidDate,
        DepositReference = c.DepositReference,
        DocumentId = c.DocumentId,
        ConfirmedAt = c.ConfirmedAt,
    };

    private static decimal? MarginOf(decimal? subtotal, decimal? cost) =>
        subtotal is > 0 && cost is not null
            ? Math.Round((subtotal.Value - cost.Value) / subtotal.Value * 100m, 2, MidpointRounding.AwayFromZero)
            : null;

    private static object Snapshot(Quote q) => new
    {
        q.Version,
        q.Status,
        q.SubtotalExVat,
        q.VatAmount,
        q.TotalIncVat,
        q.ValidUntil,
        q.ApprovedByUserId,
        Lines = q.Lines.Count,
    };
}
