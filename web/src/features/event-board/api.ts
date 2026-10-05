import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { Board, BoardColumn, CardPriority, CrewAssignment, Milestone, TaskCard } from '@/api/types';
import { useToast } from '@/components/toast/ToastContext';
import { describeCardError, moveCardInBoard, storeCard } from '@/features/boards/cards';
import { eventQuery } from '@/features/events/api';
import type { PersonOption } from '@/features/boards/PeoplePicker';

//----------------------------------------------------------\\
//                              READS
//----------------------------------------------------------\\

//crew get only the cards assigned to them (FR-21), and an event they can't see is a 404
export function useEventBoard(eventId: string) {
  return useQuery({
    queryKey: queryKeys.eventBoard(eventId),
    queryFn: () => apiFetch<Board>(`/events/${eventId}/board`),
  });
}

//the event's name and confidential flag for the page around the board. the price fields in it
//are never shown here
export function useEventSummary(eventId: string) {
  return useQuery(eventQuery(eventId));
}

//for "2h before load-in" on cards, and to link a card to a milestone
export function useMilestones(eventId: string) {
  return useQuery({
    queryKey: queryKeys.milestones(eventId),
    queryFn: () => apiFetch<Milestone[]>(`/events/${eventId}/milestones`),
    staleTime: 60_000,
  });
}

//the people a card can go to: the api only lets a card go to someone on the event's crew
export function useEventCrew(eventId: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.eventCrew(eventId),
    queryFn: () => apiFetch<CrewAssignment[]>(`/events/${eventId}/crew`),
    enabled,
    staleTime: 60_000,
  });
}

//the crew as people to pick, once each even if they work more than one shift
export function useCrewPeople(eventId: string) {
  const crew = useEventCrew(eventId);
  const people: PersonOption[] | undefined = crew.data
    ?.filter((shift, index, all) => all.findIndex((other) => other.userId === shift.userId) === index)
    .map((shift) => ({ userId: shift.userId, fullName: shift.fullName, detail: shift.crewRole }));

  return { people, failed: crew.isError };
}

//----------------------------------------------------------\\
//                              MOVE
//----------------------------------------------------------\\

export type CardMove = {
  card: TaskCard;
  to: BoardColumn;
};

//the card jumps straight away (NFR-07) and goes back with a reason if the api says no (NFR-15).
//it lands at the bottom of its new column, and the done column marks it done
export function useMoveCard(eventId: string) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const boardKey = queryKeys.eventBoard(eventId);

  return useMutation({
    mutationFn: ({ card, to }: CardMove) =>
      apiFetch<TaskCard>(`/cards/${card.cardId}/move`, {
        method: 'POST',
        json: {
          columnId: to.columnId,
          position: to.cards.filter((other) => other.cardId !== card.cardId).length,
          rowVersion: card.rowVersion,
        },
      }),

    onMutate: async ({ card, to }) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const previous = queryClient.getQueryData<Board>(boardKey);
      queryClient.setQueryData<Board>(
        boardKey,
        (board) =>
          board &&
          moveCardInBoard(board, card.cardId, to.columnId, { status: to.isDoneColumn ? 'Done' : 'Open' }),
      );
      return { previous };
    },

    onError: (error, { card }, context) => {
      if (context?.previous) queryClient.setQueryData(boardKey, context.previous);
      toast.error(describeCardError(error, card.subject));
    },

    onSuccess: (moved, { card, to }) => {
      storeCard(queryClient, moved);
      toast.success(to.isDoneColumn ? `${card.subject} is done.` : `${card.subject} moved to ${to.name}.`);
    },

    //either way, reload so the board and the card's page have the server's version
    onSettled: (_result, _error, { card }) => {
      void queryClient.invalidateQueries({ queryKey: boardKey });
      void queryClient.invalidateQueries({ queryKey: queryKeys.card(card.cardId) });
    },
  });
}

//----------------------------------------------------------\\
//                              ADD
//----------------------------------------------------------\\

export type NewCard = {
  subject: string;
  description: string | null;
  priority: CardPriority;
  dueAt: string | null;
  milestoneId: string | null;
  columnId: string;
  assigneeIds: string[];
};

//not optimistic: the dialog waits for the api, so anything it turns away lands on the right field
export function useAddCard(eventId: string, boardId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (card: NewCard) =>
      apiFetch<TaskCard>(`/boards/${boardId}/cards`, { method: 'POST', json: card }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.eventBoard(eventId) }),
  });
}
