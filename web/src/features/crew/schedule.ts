import type { CrewAssignment, EventListItem } from '@/api/types';

//how a crew member's events line up: what's next, what's after it, and what's done

//----------------------------------------------------------\\
//                              EVENTS
//----------------------------------------------------------\\

export type CrewSchedule = {
  next: EventListItem | undefined; //live now, or the soonest one coming up
  upcoming: EventListItem[]; //the rest still to come, soonest first
  past: EventListItem[]; //finished or over, most recent first
};

const isOver = (event: EventListItem, now: number) =>
  event.status === 'Finished' || new Date(event.endsAt).getTime() < now;

//cancelled events drop off: there's nothing for crew to turn up to
export function crewSchedule(events: readonly EventListItem[], now = Date.now()): CrewSchedule {
  const current = events.filter((event) => event.status !== 'Cancelled');
  const ahead = current
    .filter((event) => !isOver(event, now))
    .sort((a, b) => a.startsAt.localeCompare(b.startsAt));
  const past = current
    .filter((event) => isOver(event, now))
    .sort((a, b) => b.startsAt.localeCompare(a.startsAt));

  return { next: ahead[0], upcoming: ahead.slice(1), past };
}

//----------------------------------------------------------\\
//                              SHIFTS
//----------------------------------------------------------\\

//someone can work more than one shift on an event. the call time is when they're first needed
export function myFirstShift(crew: readonly CrewAssignment[] | undefined, userId: string) {
  return crew
    ?.filter((shift) => shift.userId === userId)
    .sort((a, b) => a.shiftStart.localeCompare(b.shiftStart))[0];
}

//the event to report against by default: the live one, else one on today, else the next one
export function defaultReportEvent(events: readonly EventListItem[], today: string, now = Date.now()) {
  const { next, upcoming } = crewSchedule(events, now);
  const open = [next, ...upcoming].filter((event): event is EventListItem => Boolean(event));
  return (
    open.find((event) => event.status === 'InProgress') ??
    open.find((event) => event.eventDate === today) ??
    open[0]
  );
}
