import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { Board, BoardColumn, CardPriority, PagedResult, TaskCard, UserListItem } from '@/api/types';
import { useToast } from '@/components/toast/ToastContext';
import { describeCardError, moveCardInBoard, storeCard } from '@/features/boards/cards';

//----------------------------------------------------------\\
//                              BOARD
//----------------------------------------------------------\\

//desk roles get every admin task, crew only the ones assigned to them (FR-21)
export function useAdminBoard(enabled = true) {
  return useQuery({
    queryKey: queryKeys.adminBoard,
    queryFn: () => apiFetch<Board>('/boards/admin'),
    enabled,
  });
}

//----------------------------------------------------------\\
//                              PEOPLE
//----------------------------------------------------------\\

//only the director and ops hand tasks out, and both hold user.manage, which this list needs
export function useActiveUsers() {
  return useQuery({
    queryKey: queryKeys.activeUsers,
    queryFn: () => apiFetch<PagedResult<UserListItem>>('/users?isActive=true&pageSize=200'),
    select: (page) =>
      page.items.filter((user) => user.isActive).sort((a, b) => a.fullName.localeCompare(b.fullName)),
    staleTime: 5 * 60_000,
  });
}

//----------------------------------------------------------\\
//                              ASSIGN
//----------------------------------------------------------\\

export type NewTask = {
  subject: string;
  description: string | null;
  priority: CardPriority;
  dueAt: string | null;
  assigneeIds: string[];
};

//not optimistic: the dialog keeps its busy button until the api has the task, so anything the
//api turns away can be shown against the field it's about
export function useAssignTask(boardId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (task: NewTask) =>
      apiFetch<TaskCard>(`/boards/${boardId}/cards`, { method: 'POST', json: task }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.adminBoard }),
  });
}

//----------------------------------------------------------\\
//                              HAND IN AND SIGN OFF
//----------------------------------------------------------\\

export type TaskStep = {
  card: TaskCard;
  action: 'handIn' | 'complete';
  to: BoardColumn;
};

//the card jumps straight away (NFR-07) and goes back with a reason if the api says no (NFR-15).
//handing in is a plain move into review. signing off has its own endpoint, so the api can check
//it's the manager who handed the task out (FR-20)
export function useTaskStep() {
  const queryClient = useQueryClient();
  const toast = useToast();

  return useMutation({
    mutationFn: ({ card, action, to }: TaskStep) =>
      action === 'handIn'
        ? apiFetch<TaskCard>(`/cards/${card.cardId}/move`, {
            method: 'POST',
            json: { columnId: to.columnId, position: to.cards.length, rowVersion: card.rowVersion },
          })
        : apiFetch<TaskCard>(`/cards/${card.cardId}/complete`, {
            method: 'POST',
            json: { rowVersion: card.rowVersion },
          }),

    onMutate: async ({ card, to }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.adminBoard });
      const previous = queryClient.getQueryData<Board>(queryKeys.adminBoard);
      queryClient.setQueryData<Board>(
        queryKeys.adminBoard,
        (board) => board && moveCardInBoard(board, card.cardId, to.columnId),
      );
      return { previous };
    },

    onError: (error, { card }, context) => {
      if (context?.previous) queryClient.setQueryData(queryKeys.adminBoard, context.previous);
      toast.error(describeCardError(error, card.subject));
    },

    onSuccess: (updated, { card, action }) => {
      storeCard(queryClient, updated);
      toast.success(
        action === 'handIn'
          ? `${card.subject} was handed in to ${card.createdBy.fullName} for review.`
          : `${card.subject} was signed off.`,
      );
    },

    //either way, reload so the board and the task's page have the server's version
    onSettled: (_result, _error, { card }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.adminBoard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.card(card.cardId) });
    },
  });
}

//----------------------------------------------------------\\
//                              RETURN
//----------------------------------------------------------\\

export type TaskReturn = {
  card: TaskCard;
  reviewNotes: string;
};

//not optimistic either: the notes are the point, so the dialog waits to hear they were saved
export function useReturnTask() {
  const queryClient = useQueryClient();
  const toast = useToast();

  return useMutation({
    mutationFn: ({ card, reviewNotes }: TaskReturn) =>
      apiFetch<TaskCard>(`/cards/${card.cardId}/return`, {
        method: 'POST',
        json: { reviewNotes, rowVersion: card.rowVersion },
      }),
    onSuccess: (updated, { card }) => {
      storeCard(queryClient, updated);
      toast.success(`${card.subject} went back to Assigned with your notes.`);
    },
    onSettled: (_result, _error, { card }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.adminBoard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.card(card.cardId) });
    },
  });
}
