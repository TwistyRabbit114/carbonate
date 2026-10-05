import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type {
  CostHistoryItem,
  EventListItem,
  Invoice,
  InvoiceStatus,
  PagedResult,
  Quote,
  RecordConfirmationRequest,
  SaveQuoteRequest,
  UpdateInvoiceRequest,
} from '@/api/types';

//C's commercial endpoints (FR-09, FR-11 to FR-15). every total, cost and margin is a field the api
//leaves out for roles that can't see it, so the screens only show what came back

//----------------------------------------------------------\\
//                              COSTINGS
//----------------------------------------------------------\\

//every costing for the event, old versions included
export function useEventQuotes(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.quotes(eventId),
    queryFn: () => apiFetch<Quote[]>(`/events/${eventId}/quotes`),
    enabled: enabled && Boolean(eventId),
  });
}

//the costing that counts: the newest version that hasn't been replaced
export function currentQuote(quotes: readonly Quote[] | undefined) {
  return quotes
    ?.filter((quote) => quote.status !== 'Superseded')
    .reduce<Quote | undefined>(
      (latest, quote) => (!latest || quote.version > latest.version ? quote : latest),
      undefined,
    );
}

//each event's current costing, one request per event
//TODO(plan): there's no endpoint that lists costings across events, so this asks event by event.
//fine for the dozen or so live at once, but ask C for GET /api/quotes if it gets slow
export function useLatestCostings(events: readonly EventListItem[]) {
  return useQueries({
    queries: events.map((event) => ({
      queryKey: queryKeys.quotes(event.eventId),
      queryFn: () => apiFetch<Quote[]>(`/events/${event.eventId}/quotes`),
    })),
  });
}

export function useQuote(quoteId: string) {
  return useQuery({
    queryKey: queryKeys.quote(quoteId),
    queryFn: () => apiFetch<Quote>(`/quotes/${quoteId}`),
  });
}

//what a costing call answers with goes straight into the cache, and the event's list is refreshed
function useStoreQuote() {
  const queryClient = useQueryClient();
  return (quote: Quote) => {
    queryClient.setQueryData(queryKeys.quote(quote.quoteId), quote);
    void queryClient.invalidateQueries({ queryKey: queryKeys.quotes(quote.eventId) });
  };
}

//a blank costing, or a copy of an earlier one for the same client (FR-15)
export function useStartCosting(eventId: string) {
  const store = useStoreQuote();

  return useMutation({
    mutationFn: (start: { sourceQuoteId: string | null }) =>
      start.sourceQuoteId
        ? apiFetch<Quote>(`/events/${eventId}/quotes/copy`, {
            method: 'POST',
            json: { sourceQuoteId: start.sourceQuoteId },
          })
        : apiFetch<Quote>(`/events/${eventId}/quotes`, { method: 'POST', json: { lines: [] } }),
    onSuccess: store,
  });
}

//saving an issued costing makes the next version, so the answer can be a different quote
export function useSaveQuote(quoteId: string) {
  const store = useStoreQuote();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (quote: SaveQuoteRequest) =>
      apiFetch<Quote>(`/quotes/${quoteId}`, { method: 'PUT', json: quote }),
    onSuccess: (saved) => {
      store(saved);
      if (saved.quoteId !== quoteId)
        void queryClient.invalidateQueries({ queryKey: queryKeys.quote(quoteId) });
    },
  });
}

export type QuoteStep = 'submit' | 'approve' | 'issue' | 'accept';

//submit for approval, approve (the director, FR-14), send to the client, and the client's yes
export function useQuoteStep() {
  const store = useStoreQuote();

  return useMutation({
    mutationFn: ({ quote, step }: { quote: Quote; step: QuoteStep }) =>
      apiFetch<Quote>(`/quotes/${quote.quoteId}/${step}`, {
        method: 'POST',
        json: { rowVersion: quote.rowVersion },
      }),
    onSuccess: store,
  });
}

//the same client's earlier events and what they cost, finance roles only (FR-09)
export function useCostHistory(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.costHistory(eventId),
    queryFn: () => apiFetch<CostHistoryItem[]>(`/events/${eventId}/cost-history`),
    enabled: enabled && Boolean(eventId),
  });
}

//----------------------------------------------------------\\
//                              CONFIRMATION
//----------------------------------------------------------\\

//a po or a deposit confirms the enquiry and puts it on the board (FR-12)
export function useRecordConfirmation(eventId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (confirmation: RecordConfirmationRequest) =>
      apiFetch<unknown>(`/events/${eventId}/confirmation`, { method: 'POST', json: confirmation }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.eventsBoard });
    },
  });
}

//----------------------------------------------------------\\
//                              INVOICES
//----------------------------------------------------------\\

//newest first, optionally only one status (FR-13)
export function useInvoices(status: InvoiceStatus | '') {
  return useQuery({
    queryKey: [...queryKeys.invoices, status || 'all'],
    queryFn: () =>
      apiFetch<PagedResult<Invoice>>(`/invoices?pageSize=200${status ? `&status=${status}` : ''}`),
    select: (page) => page.items,
  });
}

//paid with the date the money came in, or cancelled
export function useUpdateInvoice(invoiceId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (change: UpdateInvoiceRequest) =>
      apiFetch<Invoice>(`/invoices/${invoiceId}`, { method: 'PATCH', json: change }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.invoices }),
  });
}
