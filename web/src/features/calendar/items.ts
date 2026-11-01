import type { EventListItem, EventStatus, Milestone } from '@/api/types';
import { milestoneLabels } from '@/features/events/labels';
import { formatTime, sastCalendarDate } from '@/lib/format';

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

export type CalendarItem = {
  key: string;
  kind: 'event' | 'milestone';
  date: string; //the SAST day it sits on
  time: string; //"12:00", SAST
  title: string;
  eventId: string;
  at: string; //the instant, for ordering within a day
};

//----------------------------------------------------------\\
//                              WHAT SHOWS
//----------------------------------------------------------\\

//confirmed work only, the same rule as the events board (FR-03)
//TODO(plan): should enquiries show on the calendar, marked as tentative? FR-39 only says events and milestones
const shownStatuses: readonly EventStatus[] = ['ConfirmedInPlanning', 'InProgress', 'Finished'];
export const showsOnCalendar = (event: EventListItem) => shownStatuses.includes(event.status);

//----------------------------------------------------------\\
//                              BUILDING ITEMS
//----------------------------------------------------------\\

//the event on its day, then each milestone on the day it starts. doors is left out because it's
//when the event itself starts, which the event's own item already shows
export function calendarItems(events: EventListItem[], milestones: Map<string, Milestone[]>) {
  const items: CalendarItem[] = [];
  for (const event of events) {
    items.push({
      key: `event-${event.eventId}`,
      kind: 'event',
      date: event.eventDate,
      time: formatTime(event.startsAt),
      title: event.name,
      eventId: event.eventId,
      at: event.startsAt,
    });
    for (const milestone of milestones.get(event.eventId) ?? []) {
      if (milestone.milestoneType === 'Doors') continue;
      items.push({
        key: `milestone-${milestone.milestoneId}`,
        kind: 'milestone',
        date: sastCalendarDate(milestone.scheduledStart),
        time: formatTime(milestone.scheduledStart),
        title: `${milestoneLabels[milestone.milestoneType]} · ${event.name}`,
        eventId: event.eventId,
        at: milestone.scheduledStart,
      });
    }
  }
  return items;
}

//items grouped by day, earliest first within each day
export function byDay(items: CalendarItem[]) {
  const days = new Map<string, CalendarItem[]>();
  for (const item of [...items].sort((a, b) => a.at.localeCompare(b.at))) {
    days.set(item.date, [...(days.get(item.date) ?? []), item]);
  }
  return days;
}
