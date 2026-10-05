import type { DivisionCode, EventListItem, EventType } from '@/api/types';
import { sastCalendarDate } from '@/lib/format';
import { divisionLabels, eventTypeLabels } from './labels';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

//null means "any". dates are yyyy-mm-dd and both ends are included
export type BoardFilters = {
  division: DivisionCode | null;
  type: EventType | null;
  from: string | null;
  to: string | null;
};

export const noFilters: BoardFilters = { division: null, type: null, from: null, to: null };

//----------------------------------------------------------\\
//                              URL
//----------------------------------------------------------\\

//filters live in the address so a filtered board can be bookmarked or sent to someone.
//anything unrecognised is dropped, so an old or hand-edited link still opens the board
export function readBoardFilters(params: URLSearchParams): BoardFilters {
  return {
    division: oneOf(params.get('division'), divisionLabels),
    type: oneOf(params.get('type'), eventTypeLabels),
    from: calendarDate(params.get('from')),
    to: calendarDate(params.get('to')),
  };
}

export function writeBoardFilters(filters: BoardFilters) {
  const params = new URLSearchParams();
  if (filters.division) params.set('division', filters.division);
  if (filters.type) params.set('type', filters.type);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  return params;
}

//own keys only, so "?type=toString" isn't mistaken for a real value
function oneOf<T extends string>(value: string | null, known: Record<T, string>): T | null {
  return value !== null && Object.hasOwn(known, value) ? (value as T) : null;
}

function calendarDate(value: string | null) {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(value))
    ? value
    : null;
}

//----------------------------------------------------------\\
//                              MATCHING
//----------------------------------------------------------\\

export function hasFilters(filters: BoardFilters) {
  return Object.values(filters).some((value) => value !== null);
}

//an event counts if any day of it falls in the range, so a festival that started yesterday
//still shows when the range starts today
export function applyBoardFilters(events: readonly EventListItem[], filters: BoardFilters) {
  return events.filter((event) => {
    if (filters.division && event.divisionCode !== filters.division) return false;
    if (filters.type && event.eventType !== filters.type) return false;

    const lastDay = sastCalendarDate(event.endsAt);
    const ends = lastDay > event.eventDate ? lastDay : event.eventDate;
    if (filters.from && ends < filters.from) return false;
    if (filters.to && event.eventDate > filters.to) return false;
    return true;
  });
}
