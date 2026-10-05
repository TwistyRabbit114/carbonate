import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { isApiError } from '@/api/problem';
import { queryKeys } from '@/api/queryKeys';
import type {
  AllowedTransitions,
  ClientOption,
  CrewAssignment,
  Division,
  EventDetail,
  EventListItem,
  EventStatus,
  PagedResult,
  SaveEventRequest,
  ScheduleResult,
  UpdateEventRequest,
} from '@/api/types';
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
//                              ONE EVENT
//----------------------------------------------------------\\

//the full record. money on it is left out for roles that can't see it, so whatever renders it
//only shows what's there
export function eventQuery(eventId: string) {
  return queryOptions({
    queryKey: queryKeys.event(eventId),
    queryFn: () => apiFetch<EventDetail>(`/events/${eventId}`),
  });
}

export function useEvent(eventId: string) {
  return useQuery(eventQuery(eventId));
}

//every event for the desk, only their own for crew (FR-36)
export function useMyEvents() {
  return useQuery({
    queryKey: queryKeys.myEvents,
    queryFn: () => apiFetch<PagedResult<EventListItem>>('/events?pageSize=200'),
    select: (page) => page.items,
  });
}

//a 409 carries the event as it is now (NFR-15)
export function currentEventIn(error: unknown): EventDetail | null {
  if (!isApiError(error) || error.status !== 409) return null;
  const current = error.problem.current;
  return typeof current === 'object' && current !== null && 'eventId' in current && 'rowVersion' in current
    ? (current as EventDetail)
    : null;
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
      apiFetch<EventDetail>(`/events/${event.eventId}/transitions`, {
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

    onSuccess: (moved, { event, to }) => {
      queryClient.setQueryData(queryKeys.event(event.eventId), moved);
      toast.success(
        to === 'Cancelled' ? `${event.name} was cancelled.` : `${event.name} moved to ${stageLabels[to]}.`,
      );
    },

    //either way, reload so the board has the server's version and fresh row versions
    onSettled: (_result, _error, { event }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.eventsBoard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.event(event.eventId) });
    },
  });
}

//----------------------------------------------------------\\
//                              CREATE, EDIT AND DELETE
//----------------------------------------------------------\\

//not optimistic: the form waits for the api, so anything it turns away lands on the right field.
//a new event starts as an enquiry, with its milestones, board and stock list set up (FR-01, FR-25)
export function useCreateEvent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (event: SaveEventRequest) =>
      apiFetch<EventDetail>('/events', { method: 'POST', json: event }),
    onSuccess: (created) => {
      queryClient.setQueryData(queryKeys.event(created.eventId), created);
      void queryClient.invalidateQueries({ queryKey: queryKeys.myEvents });
    },
  });
}

export function useUpdateEvent(eventId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (event: UpdateEventRequest) =>
      apiFetch<EventDetail>(`/events/${eventId}`, { method: 'PUT', json: event }),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.event(eventId), saved);
      //new times can move the milestones and the cards that hang off them
      void queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.eventsBoard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.myEvents });
    },
  });
}

//a soft delete, director only. the record is kept but stops showing (FR-09)
export function useDeleteEvent(eventId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => apiFetch<void>(`/events/${eventId}`, { method: 'DELETE' }),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: queryKeys.event(eventId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.eventsBoard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.myEvents });
    },
  });
}

//----------------------------------------------------------\\
//                              MILESTONES
//----------------------------------------------------------\\

export type Reschedule = {
  milestoneId: string;
  newStart: string;
  newEnd: string;
  rowVersion: string; //the event's: a reschedule moves the event on a version too
};

//the answer lists every milestone that moved, so the dialog can say what the cascade did (FR-04)
export function useReschedule(eventId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ milestoneId, ...change }: Reschedule) =>
      apiFetch<ScheduleResult>(`/events/${eventId}/milestones/${milestoneId}/reschedule`, {
        method: 'POST',
        json: change,
      }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) }),
  });
}

//----------------------------------------------------------\\
//                              CREW
//----------------------------------------------------------\\

export type NewShift = {
  userId: string;
  crewRole: string;
  shiftStart: string;
  shiftEnd: string;
};

export function useAssignCrew(eventId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (shift: NewShift) =>
      apiFetch<CrewAssignment>(`/events/${eventId}/crew`, { method: 'POST', json: shift }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.eventCrew(eventId) }),
  });
}

export function useRemoveCrew(eventId: string) {
  const queryClient = useQueryClient();
  const toast = useToast();

  return useMutation({
    mutationFn: (shift: CrewAssignment) =>
      apiFetch<void>(`/events/${eventId}/crew/${shift.assignmentId}`, { method: 'DELETE' }),
    onSuccess: (_result, shift) => toast.success(`${shift.fullName} is off the crew.`),
    onError: (_error, shift) => toast.error(`${shift.fullName} couldn't be taken off the crew. Try again.`),
    onSettled: () => void queryClient.invalidateQueries({ queryKey: queryKeys.eventCrew(eventId) }),
  });
}

//----------------------------------------------------------\\
//                              LOOKUPS
//----------------------------------------------------------\\

//TODO(plan): GET /api/clients and GET /api/divisions aren't in the contract yet. these are the
//shapes asked of C, so the form works against the mock until the real ones land.
//a select of every client is fine at about 40 clients, a search can come if the list grows
export function useClients() {
  return useQuery({
    queryKey: queryKeys.clients,
    queryFn: () => apiFetch<PagedResult<ClientOption>>('/clients?pageSize=200'),
    select: (page) => page.items,
    staleTime: 5 * 60_000,
  });
}

export function useDivisions() {
  return useQuery({
    queryKey: queryKeys.divisions,
    queryFn: () => apiFetch<Division[]>('/divisions'),
    staleTime: Infinity,
  });
}
