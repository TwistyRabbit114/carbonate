import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { AllowedTransitions, EventListItem, EventStatus, PagedResult } from '@/api/types';
import { useToast } from '@/components/toast/ToastContext';
import { stageLabels } from './labels';
import { describeMoveError } from './moves';

//----------------------------------------------------------\\
//                              EVENTS BOARD
//----------------------------------------------------------\\

//board=true leaves out enquired and cancelled events (FR-03)
//TODO(plan): finished events aren't capped to a recent window and records are kept forever, so
//the Finished column only grows. ask C whether the board should stop at the last few weeks
export function useEventsBoard() {
  return useQuery({
    queryKey: queryKeys.eventsBoard,
    queryFn: () => apiFetch<PagedResult<EventListItem>>('/events?board=true&pageSize=200'),
    select: (page) => page.items,
    //the server moves events at their start and end times, this picks that up within a minute
    refetchInterval: 60_000,
  });
}

//----------------------------------------------------------\\
//                              ALLOWED MOVES
//----------------------------------------------------------\\

//the lifecycle rules live on the server (D's state machine), the board only asks what's allowed.
//fetched when someone reaches for a card rather than for every card up front
export function allowedTransitionsQuery(eventId: string) {
  return queryOptions({
    queryKey: queryKeys.allowedTransitions(eventId),
    queryFn: () => apiFetch<AllowedTransitions>(`/events/${eventId}/allowed-transitions`),
    select: (transitions): EventStatus[] => transitions.allowed,
    staleTime: 60_000,
  });
}

export function useAllowedTransitions(eventId: string | null) {
  return useQuery({ ...allowedTransitionsQuery(eventId ?? ''), enabled: eventId !== null });
}

//----------------------------------------------------------\\
//                              MOVE
//----------------------------------------------------------\\

export type EventMove = {
  event: EventListItem;
  to: EventStatus;
};

//the card jumps straight away (NFR-07) and goes back with a reason if the server says no (NFR-15)
export function useMoveEvent() {
  const queryClient = useQueryClient();
  const toast = useToast();

  return useMutation({
    mutationFn: ({ event, to }: EventMove) =>
      apiFetch<unknown>(`/events/${event.eventId}/transitions`, {
        method: 'POST',
        json: { to, rowVersion: event.rowVersion },
      }),

    onMutate: async ({ event, to }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.eventsBoard });
      const previous = queryClient.getQueryData<PagedResult<EventListItem>>(queryKeys.eventsBoard);
      queryClient.setQueryData<PagedResult<EventListItem>>(queryKeys.eventsBoard, (page) =>
        page
          ? {
              ...page,
              items: page.items.map((item) =>
                item.eventId === event.eventId ? { ...item, status: to } : item,
              ),
            }
          : page,
      );
      return { previous };
    },

    onError: (error, move, context) => {
      if (context?.previous) queryClient.setQueryData(queryKeys.eventsBoard, context.previous);
      toast.error(describeMoveError(error, move.event.name));
    },

    onSuccess: (_result, { event, to }) => {
      toast.success(
        to === 'Cancelled' ? `${event.name} was cancelled.` : `${event.name} moved to ${stageLabels[to]}.`,
      );
    },

    //either way, reload so the board has the server's version and fresh row versions
    onSettled: (_result, _error, { event }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.eventsBoard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.allowedTransitions(event.eventId) });
    },
  });
}
