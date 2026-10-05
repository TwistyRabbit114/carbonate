import { describe, expect, it } from 'vitest';
import type { CrewAssignment, EventListItem } from '@/api/types';
import { demoCrew } from '@/test/fixtures/eventDetails';
import { demoEvents } from '@/test/fixtures/events';
import { users } from '@/test/fixtures/users';
import { crewSchedule, defaultReportEvent, myFirstShift } from './schedule';

const events = demoEvents();
const byCode = (code: string) => events.find((event) => event.eventCode === code)!;

describe('crewSchedule', () => {
  it('puts the live event first, then the rest soonest first', () => {
    const { next, upcoming } = crewSchedule(events);
    expect(next?.eventCode).toBe('RIV-FEST-26');
    expect(upcoming.map((event) => event.eventCode)).toEqual([
      'VAN-ACT-26',
      'NAI-WED-26',
      'MER-YE-26',
      'ATL-LNCH-26',
    ]);
  });

  it('keeps finished events apart, most recent first', () => {
    expect(crewSchedule(events).past.map((event) => event.eventCode)).toEqual(['DEL-GOLF-26']);
  });

  it('drops cancelled events', () => {
    const cancelled: EventListItem = { ...byCode('VAN-ACT-26'), status: 'Cancelled' };
    const { next, upcoming, past } = crewSchedule([cancelled]);
    expect([next, ...upcoming, ...past].filter(Boolean)).toEqual([]);
  });

  it('has nothing next when there are no events', () => {
    expect(crewSchedule([]).next).toBeUndefined();
  });
});

describe('myFirstShift', () => {
  const crew = demoCrew(byCode('NAI-WED-26'));

  it("finds the person's own shift", () => {
    expect(myFirstShift(crew, users.casualCrew.user.userId)?.crewRole).toBe('Bartender');
  });

  it('takes the earliest when they work more than one', () => {
    const early: CrewAssignment = {
      ...crew[1]!,
      assignmentId: 'early',
      shiftStart: new Date(new Date(crew[1]!.shiftStart).getTime() - 3_600_000).toISOString(),
    };
    expect(myFirstShift([...crew, early], users.casualCrew.user.userId)?.assignmentId).toBe('early');
  });

  it("is empty for someone who isn't on the crew", () => {
    expect(myFirstShift(crew, users.director.user.userId)).toBeUndefined();
  });
});

describe('defaultReportEvent', () => {
  it('picks the live event when there is one', () => {
    expect(defaultReportEvent(events, '2000-01-01')?.eventCode).toBe('RIV-FEST-26');
  });

  it("picks today's event when nothing is live", () => {
    const notLive = events.filter((event) => event.status !== 'InProgress');
    const wedding = byCode('NAI-WED-26');
    expect(defaultReportEvent(notLive, wedding.eventDate)?.eventCode).toBe('NAI-WED-26');
  });
});
