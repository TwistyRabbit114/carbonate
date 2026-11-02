import { keepPreviousData, useQueries, useQuery } from '@tanstack/react-query';
import { apiFetch } from '@/api/client';
import { queryKeys } from '@/api/queryKeys';
import type { EventListItem, Milestone, PagedResult } from '@/api/types';
import { milestonesQuery } from '@/features/event-board/api';
import { addDays } from './calendarDates';
import { byDay, calendarItems, showsOnCalendar } from './items';

//----------------------------------------------------------\\
//                              QUERIES
//----------------------------------------------------------\\

//a recce can be weeks before the event and a reconciliation a week after it, so events up to a
//month either side are fetched for their milestones. the list is filtered on the event's date
const margin = 31;

//everything on the calendar between two days, from the events list and each event's milestones,
//so there's no endpoint just for this (FR-39). the api already leaves crew with their own events
export function useCalendarItems(first: string, last: string) {
  const [from, to] = [addDays(first, -margin), addDays(last, margin)];
  const events = useQuery({
    queryKey: queryKeys.calendarEvents(from, to),
    queryFn: () => apiFetch<PagedResult<EventListItem>>(`/events?from=${from}&to=${to}&pageSize=200`),
    select: (page) => page.items.filter(showsOnCalendar),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  });

  const listed = events.data ?? [];
  const milestones = useQueries({
    queries: listed.map((event) => milestonesQuery(event.eventId)),
  });

  const loaded = new Map<string, Milestone[]>();
  milestones.forEach((query, i) => {
    const event = listed[i];
    if (query.data && event) loaded.set(event.eventId, query.data);
  });
  const missing = milestones.filter((query) => query.isError);

  return {
    days: byDay(calendarItems(listed, loaded)),
    isPending: events.isPending,
    isError: events.isError,
    refetch: () => void events.refetch(),
    milestonesFailed: missing.length > 0,
    retryMilestones: () => missing.forEach((query) => void query.refetch()),
  };
}
