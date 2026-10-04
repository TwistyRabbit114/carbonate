import { useQuery } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { EventListItem, PagedResult } from '@/api/types';

//----------------------------------------------------------\\
//                              EVENTS BOARD
//----------------------------------------------------------\\

//TODO(plan): view=board is assumed to mean "active stages only, no enquired", confirm with C.
//also whether finished events are capped to a recent window, since records are kept forever
export function useEventsBoard() {
  return useQuery({
    queryKey: queryKeys.eventsBoard,
    queryFn: () => apiFetch<PagedResult<EventListItem>>('/events?view=board&pageSize=200'),
    select: (page) => page.items,
    //the server moves events at their start and end times, this picks that up within a minute
    refetchInterval: 60_000,
  });
}
