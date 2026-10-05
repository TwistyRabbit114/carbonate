import {
  queryOptions,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
  type QueryKey,
} from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { isApiError } from '@/api/problem';
import { queryKeys } from '@/api/queryKeys';
import type { Board, CardPriority, TaskCard } from '@/api/types';

//what both boards share about a single card: reading it, editing it, and who's on it

//----------------------------------------------------------\\
//                              READ
//----------------------------------------------------------\\

//a card the user can't see is a 404 from the api, the same as one that doesn't exist
export function cardQuery(cardId: string) {
  return queryOptions({
    queryKey: queryKeys.card(cardId),
    queryFn: () => apiFetch<TaskCard>(`/cards/${cardId}`),
  });
}

export function useCard(cardId: string) {
  return useQuery(cardQuery(cardId));
}

//the card as the api last sent it, so its page shows the change without waiting for a reload
export function storeCard(queryClient: QueryClient, card: TaskCard) {
  queryClient.setQueryData(queryKeys.card(card.cardId), card);
}

//a 409 carries the card as it is now (NFR-15)
export function currentCardIn(error: unknown): TaskCard | null {
  if (!isApiError(error) || error.status !== 409) return null;
  const current = error.problem.current;
  return typeof current === 'object' && current !== null && 'cardId' in current && 'rowVersion' in current
    ? (current as TaskCard)
    : null;
}

//----------------------------------------------------------\\
//                              EDIT
//----------------------------------------------------------\\

export const cardPriorities = [
  'Low',
  'Normal',
  'High',
  'Critical',
] as const satisfies readonly CardPriority[];

//the api replaces every editable field, so a field left out is cleared
export type CardFields = {
  subject: string;
  description: string | null;
  priority: CardPriority;
  dueAt: string | null;
  milestoneId: string | null;
};

export type CardEdit = {
  card: TaskCard;
  rowVersion: string; //the version the form started from, not whatever has loaded since
  fields: CardFields;
};

//not optimistic: the dialog keeps its busy button until the api has the change, so anything
//it turns away can be shown against the field it's about
export function useUpdateCard(boardKey: QueryKey) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ card, rowVersion, fields }: CardEdit) =>
      apiFetch<TaskCard>(`/cards/${card.cardId}`, { method: 'PATCH', json: { ...fields, rowVersion } }),
    onSuccess: (card) => storeCard(queryClient, card),
    onError: (error) => {
      const current = currentCardIn(error);
      if (current) storeCard(queryClient, current);
    },
    onSettled: () => void queryClient.invalidateQueries({ queryKey: boardKey }),
  });
}

//----------------------------------------------------------\\
//                              ASSIGNEES
//----------------------------------------------------------\\

export type AssigneeChange = {
  card: TaskCard;
  userIds: string[];
};

//the whole set of people, replacing whoever was on the card, at the version the page has
export function useSetAssignees(boardKey: QueryKey) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ card, userIds }: AssigneeChange) =>
      apiFetch<TaskCard>(`/cards/${card.cardId}/assignees`, {
        method: 'PUT',
        json: { userIds, rowVersion: card.rowVersion },
      }),
    onSuccess: (card) => storeCard(queryClient, card),
    onError: (error) => {
      const current = currentCardIn(error);
      if (current) storeCard(queryClient, current);
    },
    onSettled: () => void queryClient.invalidateQueries({ queryKey: boardKey }),
  });
}

//----------------------------------------------------------\\
//                              BOARD CACHE
//----------------------------------------------------------\\

//the cached board with one card moved to the bottom of another column, for an optimistic move
export function moveCardInBoard(
  board: Board,
  cardId: string,
  toColumnId: string,
  changes: Partial<TaskCard> = {},
): Board {
  const card = board.columns
    .flatMap((column) => column.cards)
    .find((candidate) => candidate.cardId === cardId);
  if (!card) return board;

  return {
    ...board,
    columns: board.columns.map((column) => {
      const others = column.cards.filter((candidate) => candidate.cardId !== cardId);
      if (column.columnId !== toColumnId) return { ...column, cards: others };
      return { ...column, cards: [...others, { ...card, ...changes, columnId: toColumnId }] };
    }),
  };
}

//----------------------------------------------------------\\
//                              DUE DATES
//----------------------------------------------------------\\

//TODO(plan): admin task due dates are picked as a day and sent as 17:00 SAST, the end of the working
//day. confirm with D whether the calendar should show them as all-day entries instead
export const endOfWorkingDay = (date: string) => (date ? `${date}T17:00:00+02:00` : null);

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

//plain words for why a change didn't happen. on a board the card is already back where it was
export function describeCardError(error: unknown, subject: string) {
  if (!isApiError(error)) return `Something went wrong, so nothing changed on ${subject}.`;
  if (error.status === 0) return `Couldn't reach Carbonate, so nothing changed on ${subject}.`;
  if (error.status === 409 && error.problem.type?.endsWith('/concurrency-conflict')) {
    return `Someone else changed ${subject} just now, so nothing changed. You're looking at their change now.`;
  }
  //the api says why: not theirs to sign off, not at that stage yet, or not on the event's crew
  if ([403, 409, 422].includes(error.status) && error.problem.detail) return error.problem.detail;
  if (error.status === 403) return `Your role can't do that on ${subject}.`;
  if (error.status === 404) return `${subject} isn't on the board any more.`;
  return `Something went wrong, so nothing changed on ${subject}.`;
}
