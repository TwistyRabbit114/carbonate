import type { CrewAssignment, EventDetail, EventListItem, Milestone, MilestoneType } from '@/api/types';
import { demoEvents } from './events';
import { users } from './users';

//what the event pages need beyond the board's list row: the full record, the milestone chain and
//the crew. worked out from the demo events, so the dates line up with theirs

//----------------------------------------------------------\\
//                              DETAIL
//----------------------------------------------------------\\

const clients: Record<string, string> = {
  'NAI-WED-26': 'Naidoo family',
  'VAN-ACT-26': 'Vantage Beverages',
  'MER-YE-26': 'Meridian Holdings',
  'RIV-FEST-26': 'Riverlight Events Trust',
  'DEL-GOLF-26': 'Delacroix Group',
  'ATL-LNCH-26': 'Atlas Brands',
};

const divisionIds = {
  CE: 'd1000000-0000-0000-0000-0000000000ce',
  CLM: 'd1000000-0000-0000-0000-00000000c1a0',
};

//no budget here: it's a $price field, and fixtures for a role that holds it add it themselves
export function eventDetailFor(event: EventListItem): EventDetail {
  const number = event.eventId.slice(-1);
  return {
    ...event,
    clientId: `c1000000-0000-0000-0000-00000000000${number}`,
    clientName: clients[event.eventCode] ?? 'Demo client',
    venueId: event.venueName ? `5e000000-0000-0000-0000-00000000000${number}` : null,
    divisionId: divisionIds[event.divisionCode as keyof typeof divisionIds] ?? divisionIds.CE,
    createdByUserId: users.eventManager.user.userId,
    paymentMode: event.eventType === 'Wedding' ? 'Deposit' : 'PurchaseOrder',
    infrastructureMode: 'Owned',
    staffRequired: Math.max(2, Math.ceil(event.packSizeEstimated / 60)),
    createdAt: new Date(Date.now() - 30 * 24 * 3_600_000).toISOString(),
    isActive: true,
  };
}

export const demoEventDetail = (eventId: string) => {
  const event = demoEvents().find((candidate) => candidate.eventId === eventId);
  return event ? eventDetailFor(event) : undefined;
};

//----------------------------------------------------------\\
//                              MILESTONES
//----------------------------------------------------------\\

const hour = 3_600_000;

//the default chain from plan section 8.2, recce to reconciliation. doors comes from the event's start
//and strike from its end, the rest sit around them
const chain: { type: MilestoneType; from: 'start' | 'end'; offsetHours: number; hours: number }[] = [
  { type: 'SiteVisit', from: 'start', offsetHours: -7 * 24, hours: 2 },
  { type: 'LoadIn', from: 'start', offsetHours: -6, hours: 4 },
  { type: 'Rehearsal', from: 'start', offsetHours: -2, hours: 1 },
  { type: 'Doors', from: 'start', offsetHours: 0, hours: 1 },
  { type: 'Strike', from: 'end', offsetHours: 0, hours: 3 },
  { type: 'LoadOut', from: 'end', offsetHours: 3, hours: 2 },
  { type: 'Debrief', from: 'end', offsetHours: 2 * 24, hours: 1 },
  { type: 'Invoice', from: 'end', offsetHours: 3 * 24, hours: 1 },
  { type: 'Reconciliation', from: 'end', offsetHours: 7 * 24, hours: 2 },
];

export function demoMilestones(event: EventListItem): Milestone[] {
  const number = event.eventId.slice(-1);
  const now = Date.now();

  return chain.map((step, index) => {
    const anchor = new Date(step.from === 'start' ? event.startsAt : event.endsAt).getTime();
    const start = anchor + step.offsetHours * hour;
    const end = start + step.hours * hour;
    return {
      milestoneId: `1a00000${number}-0000-0000-0000-00000000000${index}`,
      eventId: event.eventId,
      milestoneType: step.type,
      scheduledStart: new Date(start).toISOString(),
      scheduledEnd: new Date(end).toISOString(),
      actualStart: start < now ? new Date(start).toISOString() : null,
      actualEnd: end < now ? new Date(end).toISOString() : null,
      status: end < now ? 'Done' : start < now ? 'InProgress' : 'Planned',
      predecessorIds: index === 0 ? [] : [`1a00000${number}-0000-0000-0000-00000000000${index - 1}`],
    };
  });
}

//the milestone of a type, for cards that hang off it
export const milestoneOf = (milestones: Milestone[], type: MilestoneType) =>
  milestones.find((milestone) => milestone.milestoneType === type)!;

//----------------------------------------------------------\\
//                              CREW
//----------------------------------------------------------\\

//thabo leads the bar and priya works it, on every event but the enquiry. no hourly rates: those
//are $staff, and fixtures for a role that sees them add them
export function demoCrew(event: EventListItem): CrewAssignment[] {
  if (event.status === 'Enquired') return [];
  const number = event.eventId.slice(-1);
  const shift = (index: number, account: 'crewLead' | 'casualCrew', crewRole: string): CrewAssignment => ({
    assignmentId: `c4e0000${number}-0000-0000-0000-00000000000${index}`,
    eventId: event.eventId,
    userId: users[account].user.userId,
    fullName: users[account].user.fullName,
    crewRole,
    shiftStart: new Date(new Date(event.startsAt).getTime() - 6 * hour).toISOString(),
    shiftEnd: new Date(new Date(event.endsAt).getTime() + 3 * hour).toISOString(),
    confirmed: true,
  });

  return [shift(1, 'crewLead', 'Bar lead'), shift(2, 'casualCrew', 'Bartender')];
}
