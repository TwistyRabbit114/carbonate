import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { Quote, RecordConfirmationRequest } from '@/api/types';

//C's commercial endpoints, read here for the event page

//every costing for the event, old versions included. the money on them is only there for
//roles that can see it
export function useEventQuotes(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.quotes(eventId),
    queryFn: () => apiFetch<Quote[]>(`/events/${eventId}/quotes`),
    enabled,
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
