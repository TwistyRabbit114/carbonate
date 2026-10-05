import { delay, http, HttpResponse } from 'msw';
import type { CostHistoryItem, Invoice, Quote, QuoteLine, QuoteLineCategory, QuoteStatus } from '@/api/types';
import { approvalThreshold } from '../fixtures/commercial';
import {
  businessRule,
  caller,
  forbidden,
  invalid,
  invalidTransition,
  mock,
  nextRowVersion,
  notBuilt,
  notFound,
  numberOrNull,
  readJson,
  stale,
  trimmed,
  unauthorised,
  visibleEvent,
  withoutKeys,
  type Caller,
} from './mockCore';

//C's commercial cycle: costings, approval, cost history and invoices (FR-09, FR-11 to FR-15),
//with the api's rules and wording. totals are always worked out here, never taken from the client

//----------------------------------------------------------\\
//                              MONEY
//----------------------------------------------------------\\

const priceKeys = ['subtotalExVat', 'vatAmount', 'totalIncVat'];
const costKeys = ['internalCostTotal'];
const marginKeys = ['marginPercent', 'targetMarginBand'];

//each tier comes off for a role without it (plan section 7.3). in practice the three travel
//together, but the mock doesn't rely on that
export function quoteFor(who: Caller, quote: Quote): Quote {
  const hidden = [
    ...(who.can('finance.view_client_price') ? [] : priceKeys),
    ...(who.can('finance.view_internal_cost') ? [] : costKeys),
    ...(who.can('finance.view_margin') ? [] : marginKeys),
  ];
  const hiddenOnLines = [
    ...(who.can('finance.view_client_price') ? [] : ['unitPriceToClient', 'lineTotal']),
    ...(who.can('finance.view_internal_cost') ? [] : ['unitCostToUs']),
  ];
  return {
    ...withoutKeys(quote, hidden),
    lines: quote.lines.map((line) => withoutKeys(line, hiddenOnLines)),
  };
}

const invoiceFor = (who: Caller, invoice: Invoice): Invoice =>
  who.can('finance.view_client_price') ? invoice : withoutKeys(invoice, ['amountIncVat']);

const round = (value: number) => Math.round(value * 100) / 100;
const vatRate = 0.15;

const categories: QuoteLineCategory[] = [
  'SetUpAndStrike',
  'Infrastructure',
  'Transportation',
  'Crew',
  'Ice',
  'Stock',
  'BarKit',
  'Glassware',
  'Other',
];

type CheckedLines = {
  lines: QuoteLine[];
  totals: Pick<Quote, 'subtotalExVat' | 'vatAmount' | 'totalIncVat' | 'internalCostTotal' | 'marginPercent'>;
};

function checkLines(body: Record<string, unknown>): CheckedLines | Response {
  const sent = Array.isArray(body.lines) ? (body.lines as Record<string, unknown>[]) : [];
  const errors: Record<string, string[]> = {};
  sent.forEach((line, index) => {
    const quantity = numberOrNull(line.quantity);
    const price = numberOrNull(line.unitPriceToClient);
    const cost = numberOrNull(line.unitCostToUs);
    if (!trimmed(line.description))
      errors[`lines[${index}].description`] = ["'Description' must not be empty."];
    if (quantity === null || quantity <= 0)
      errors[`lines[${index}].quantity`] = ["'Quantity' must be greater than '0'."];
    if (!categories.includes(line.category as QuoteLineCategory))
      errors[`lines[${index}].category`] = ['Choose a category.'];
    if (price === null || price < 0)
      errors[`lines[${index}].unitPriceToClient`] = ['Enter the price to the client.'];
    if (cost === null || cost < 0) errors[`lines[${index}].unitCostToUs`] = ['Enter what this costs us.'];
  });
  if (Object.keys(errors).length > 0) return invalid(errors);

  const lines: QuoteLine[] = sent.map((line) => {
    const quantity = line.quantity as number;
    const price = line.unitPriceToClient as number;
    return {
      quoteLineId: crypto.randomUUID(),
      description: trimmed(line.description),
      quantity,
      category: line.category as QuoteLineCategory,
      unitCostToUs: line.unitCostToUs as number,
      unitPriceToClient: price,
      lineTotal: round(quantity * price),
    };
  });
  const subtotal = round(lines.reduce((total, line) => total + line.lineTotal!, 0));
  const cost = round(lines.reduce((total, line) => total + round(line.quantity * line.unitCostToUs!), 0));
  const vat = round(subtotal * vatRate);
  return {
    lines,
    totals: {
      subtotalExVat: subtotal,
      vatAmount: vat,
      totalIncVat: round(subtotal + vat),
      internalCostTotal: cost,
      marginPercent: subtotal > 0 ? round(((subtotal - cost) / subtotal) * 100) : null,
    },
  };
}

function newQuote(
  eventId: string,
  checked: CheckedLines,
  copiedFromQuoteId: string | null,
  validUntil: unknown,
): Quote {
  const version =
    Math.max(0, ...mock.quotes.filter((quote) => quote.eventId === eventId).map((quote) => quote.version)) +
    1;
  return {
    quoteId: crypto.randomUUID(),
    eventId,
    copiedFromQuoteId,
    version,
    status: 'Draft',
    ...checked.totals,
    targetMarginBand: { targetMinPct: 28, targetMaxPct: 35 },
    requiresApproval: checked.totals.totalIncVat! > approvalThreshold,
    validUntil: typeof validUntil === 'string' && validUntil ? validUntil : null,
    issuedAt: null,
    acceptedAt: null,
    approvedByUserId: null,
    approvedAt: null,
    createdAt: new Date().toISOString(),
    rowVersion: nextRowVersion(),
    lines: checked.lines,
  };
}

const save = (quote: Quote) => {
  mock.quotes = mock.quotes.some((candidate) => candidate.quoteId === quote.quoteId)
    ? mock.quotes.map((candidate) => (candidate.quoteId === quote.quoteId ? quote : candidate))
    : [...mock.quotes, quote];
  return quote;
};

//a costing the caller may open: they need quote.view and the event has to be visible to them
function visibleQuote(who: Caller, quoteId: unknown) {
  const quote = mock.quotes.find((candidate) => candidate.quoteId === quoteId);
  return quote && visibleEvent(who, quote.eventId) ? quote : undefined;
}

const staleQuote = (who: Caller, quote: Quote) =>
  stale(
    'Someone else changed this costing.',
    'Reload the costing to see their changes, then make yours again.',
    quoteFor(who, quote),
  );

//----------------------------------------------------------\\
//                              QUOTE ACTIONS
//----------------------------------------------------------\\

type Action = 'submit' | 'approve' | 'issue' | 'accept';

function act(who: Caller, quote: Quote, action: Action): Quote | Response {
  const now = new Date().toISOString();
  const approved = (): Quote => ({
    ...quote,
    status: 'Approved',
    requiresApproval: false,
    approvedByUserId: who.me.userId,
    approvedAt: now,
  });
  const needsLines = () =>
    quote.lines.length === 0 ? businessRule('Add at least one line to the costing first.') : null;

  if (action === 'submit') {
    if (quote.status !== 'Draft')
      return invalidTransition('Only a draft costing can be submitted for approval.');
    //a costing the director wrote counts as approved as soon as it's submitted (FR-14)
    return needsLines() ?? (who.can('quote.approve') ? approved() : { ...quote, status: 'PendingApproval' });
  }
  if (action === 'approve') {
    if (quote.status !== 'PendingApproval')
      return invalidTransition('Only a costing that is waiting for approval can be approved.');
    return approved();
  }
  if (action === 'issue') {
    const issuable: QuoteStatus[] = ['Draft', 'PendingApproval', 'Approved'];
    if (!issuable.includes(quote.status))
      return invalidTransition('This costing has already been issued or replaced.');
    if (quote.requiresApproval && !quote.approvedByUserId) {
      return businessRule(
        'This costing is above the approval limit, so the Director must approve it before it is issued.',
      );
    }
    return needsLines() ?? { ...quote, status: 'Issued', issuedAt: now };
  }
  if (quote.status !== 'Issued') return invalidTransition('Only an issued costing can be accepted.');
  return { ...quote, status: 'Accepted', acceptedAt: now };
}

async function quoteAction(request: Request, quoteId: unknown, action: Action) {
  await delay();
  const who = caller(request);
  if (!who) return unauthorised();
  if (!who.can(action === 'approve' ? 'quote.approve' : 'quote.edit')) return forbidden();
  const quote = visibleQuote(who, quoteId);
  if (!quote) return notFound('That costing was not found.');

  const { rowVersion } = await readJson(request);
  if (rowVersion !== quote.rowVersion) return staleQuote(who, quote);
  const result = act(who, quote, action);
  if (result instanceof Response) return result;
  return HttpResponse.json(quoteFor(who, save({ ...result, rowVersion: nextRowVersion() })));
}

//----------------------------------------------------------\\
//                              HANDLERS
//----------------------------------------------------------\\

export const financeHandlers = [
  //newest version first
  http.get('/api/events/:eventId/quotes', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('quote.view')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();
    return HttpResponse.json(
      mock.quotes
        .filter((quote) => quote.eventId === event.eventId)
        .sort((a, b) => b.version - a.version)
        .map((quote) => quoteFor(who, quote)),
    );
  }),

  http.post('/api/events/:eventId/quotes', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('quote.edit')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const body = await readJson(request);
    const checked = checkLines(body);
    if (checked instanceof Response) return checked;
    return HttpResponse.json(quoteFor(who, save(newQuote(event.eventId, checked, null, body.validUntil))), {
      status: 201,
    });
  }),

  //prices are the client's, so a costing only copies between events for the same client (FR-15)
  http.post('/api/events/:eventId/quotes/copy', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('quote.edit')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const { sourceQuoteId } = await readJson(request);
    const source = visibleQuote(who, sourceQuoteId);
    if (!source) return notFound('That costing was not found.');
    const sourceEvent = mock.events.find((candidate) => candidate.eventId === source.eventId);
    if (sourceEvent?.clientId !== event.clientId) {
      return businessRule('You can only copy a costing from an earlier event for the same client.');
    }

    const checked = checkLines({ lines: source.lines });
    if (checked instanceof Response) return checked;
    return HttpResponse.json(quoteFor(who, save(newQuote(event.eventId, checked, source.quoteId, null))), {
      status: 201,
    });
  }),

  http.get('/api/quotes/:quoteId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('quote.view')) return forbidden();
    const quote = visibleQuote(who, params.quoteId);
    return quote ? HttpResponse.json(quoteFor(who, quote)) : notFound('That costing was not found.');
  }),

  //an issued costing never changes: editing it makes the next version and supersedes it. an
  //approved one goes back to draft, because the approval no longer covers the new numbers
  http.put('/api/quotes/:quoteId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('quote.edit')) return forbidden();
    const quote = visibleQuote(who, params.quoteId);
    if (!quote) return notFound('That costing was not found.');
    if (quote.status === 'Accepted' || quote.status === 'Superseded') {
      return businessRule('A costing that has been accepted or replaced cannot be changed. Start a new one.');
    }

    const body = await readJson(request);
    if (body.rowVersion !== quote.rowVersion) return staleQuote(who, quote);
    const checked = checkLines(body);
    if (checked instanceof Response) return checked;

    if (quote.status === 'Issued') {
      save({ ...quote, status: 'Superseded', rowVersion: nextRowVersion() });
      return HttpResponse.json(quoteFor(who, save(newQuote(quote.eventId, checked, null, body.validUntil))));
    }
    const reopened = quote.status === 'PendingApproval' || quote.status === 'Approved';
    const updated: Quote = {
      ...quote,
      ...checked.totals,
      lines: checked.lines,
      validUntil: typeof body.validUntil === 'string' && body.validUntil ? body.validUntil : null,
      status: reopened ? 'Draft' : quote.status,
      approvedByUserId: reopened ? null : quote.approvedByUserId,
      approvedAt: reopened ? null : quote.approvedAt,
      requiresApproval: checked.totals.totalIncVat! > approvalThreshold,
      rowVersion: nextRowVersion(),
    };
    return HttpResponse.json(quoteFor(who, save(updated)));
  }),

  http.post('/api/quotes/:quoteId/submit', ({ request, params }) =>
    quoteAction(request, params.quoteId, 'submit'),
  ),
  http.post('/api/quotes/:quoteId/approve', ({ request, params }) =>
    quoteAction(request, params.quoteId, 'approve'),
  ),
  http.post('/api/quotes/:quoteId/issue', ({ request, params }) =>
    quoteAction(request, params.quoteId, 'issue'),
  ),
  http.post('/api/quotes/:quoteId/accept', ({ request, params }) =>
    quoteAction(request, params.quoteId, 'accept'),
  ),

  //the same client's other events, each with its accepted costing or else its latest (FR-09)
  http.get('/api/events/:eventId/cost-history', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('finance.view_client_price')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const items: CostHistoryItem[] = mock.events
      .filter(
        (other) => other.clientId === event.clientId && other.eventId !== event.eventId && other.isActive,
      )
      .sort((a, b) => b.eventDate.localeCompare(a.eventDate))
      .map((other) => {
        const chosen = mock.quotes
          .filter((quote) => quote.eventId === other.eventId && quote.status !== 'Superseded')
          .sort(
            (a, b) =>
              Number(b.status === 'Accepted') - Number(a.status === 'Accepted') || b.version - a.version,
          )[0];
        return {
          eventId: other.eventId,
          eventCode: other.eventCode,
          name: other.name,
          eventDate: other.eventDate,
          packSize: other.packSizeActual ?? other.packSizeEstimated,
          totalIncVat: chosen?.totalIncVat ?? null,
          internalCostTotal: chosen?.internalCostTotal ?? null,
          marginPercent: chosen?.marginPercent ?? null,
        };
      });
    return HttpResponse.json(items);
  }),

  http.get('/api/invoices', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('invoice.view')) return forbidden();
    const status = new URL(request.url).searchParams.get('status');
    const items = mock.invoices
      .filter((invoice) => !status || invoice.status === status)
      .sort((a, b) => b.issuedDate.localeCompare(a.issuedDate))
      .map((invoice) => invoiceFor(who, invoice));
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.patch('/api/invoices/:invoiceId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('invoice.manage')) return forbidden();
    const invoice = mock.invoices.find((candidate) => candidate.invoiceId === params.invoiceId);
    if (!invoice) return notFound('That invoice was not found.');

    const { status, paidDate } = await readJson(request);
    let updated: Invoice;
    if (status === 'Paid' && invoice.status === 'Issued') {
      if (!trimmed(paidDate)) return invalid({ paidDate: ['Enter the date it was paid.'] });
      if (trimmed(paidDate) < invoice.issuedDate)
        return invalid({ paidDate: ['It cannot be paid before it was issued.'] });
      updated = { ...invoice, status: 'Paid', paidDate: trimmed(paidDate) };
    } else if (status === 'Void' && (invoice.status === 'Draft' || invoice.status === 'Issued')) {
      updated = { ...invoice, status: 'Void' };
    } else if (status === 'Issued' && invoice.status === 'Draft') {
      updated = { ...invoice, status: 'Issued' };
    } else {
      return businessRule(
        `A ${invoice.status.toLowerCase()} invoice cannot be marked ${String(status).toLowerCase()}.`,
      );
    }
    mock.invoices = mock.invoices.map((candidate) => (candidate === invoice ? updated : candidate));
    return HttpResponse.json(invoiceFor(who, updated));
  }),

  //FR-16 is still a placeholder on the api
  http.get('/api/invoices/uninvoiced-events', () => notBuilt()),
];
