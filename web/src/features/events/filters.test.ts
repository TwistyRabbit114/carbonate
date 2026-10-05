import { describe, expect, it } from 'vitest';
import type { EventListItem } from '@/api/types';
import { demoEvents } from '@/test/fixtures/events';
import {
  applyBoardFilters,
  hasFilters,
  noFilters,
  readBoardFilters,
  writeBoardFilters,
  type BoardFilters,
} from './filters';

//fixed dates, so these don't shift with the clock like the demo data does
const event = (changes: Partial<EventListItem>): EventListItem => ({ ...demoEvents()[0]!, ...changes });

const wedding = event({
  eventId: 'wedding',
  divisionCode: 'CE',
  eventType: 'Wedding',
  eventDate: '2026-12-05',
  startsAt: '2026-12-05T13:30:00Z',
  endsAt: '2026-12-05T21:30:00Z',
});

//friday to sunday
const festival = event({
  eventId: 'festival',
  divisionCode: 'CLM',
  eventType: 'Festival',
  eventDate: '2026-12-11',
  startsAt: '2026-12-11T08:00:00Z',
  endsAt: '2026-12-13T20:00:00Z',
});

//ends 01:00 on the 20th in cape town, which is still the 19th in utc
const lateParty = event({
  eventId: 'late-party',
  divisionCode: 'CE',
  eventType: 'Private',
  eventDate: '2026-12-19',
  startsAt: '2026-12-19T17:00:00Z',
  endsAt: '2026-12-19T23:00:00Z',
});

const events = [wedding, festival, lateParty];

const ids = (filters: Partial<BoardFilters>) =>
  applyBoardFilters(events, { ...noFilters, ...filters }).map((match) => match.eventId);

//----------------------------------------------------------\\
//                              URL
//----------------------------------------------------------\\

describe('board filters in the address', () => {
  it('reads every filter', () => {
    const params = new URLSearchParams('division=CLM&type=Festival&from=2026-12-01&to=2026-12-31');

    expect(readBoardFilters(params)).toEqual({
      division: 'CLM',
      type: 'Festival',
      from: '2026-12-01',
      to: '2026-12-31',
    });
  });

  it("drops anything it doesn't recognise instead of failing", () => {
    const params = new URLSearchParams('division=XYZ&type=toString&from=tomorrow&to=2026-13-45');

    expect(readBoardFilters(params)).toEqual(noFilters);
  });

  it('writes only the filters that are set, and reads them back the same', () => {
    const filters: BoardFilters = { division: 'CE', type: null, from: '2026-12-01', to: null };
    const params = writeBoardFilters(filters);

    expect(params.toString()).toBe('division=CE&from=2026-12-01');
    expect(readBoardFilters(params)).toEqual(filters);
    expect(writeBoardFilters(noFilters).toString()).toBe('');
  });

  it('knows when any filter is on', () => {
    expect(hasFilters(noFilters)).toBe(false);
    expect(hasFilters({ ...noFilters, to: '2026-12-31' })).toBe(true);
  });
});

//----------------------------------------------------------\\
//                              MATCHING
//----------------------------------------------------------\\

describe('applying board filters', () => {
  it('keeps everything with no filters', () => {
    expect(ids({})).toEqual(['wedding', 'festival', 'late-party']);
  });

  it('matches division and event type', () => {
    expect(ids({ division: 'CLM' })).toEqual(['festival']);
    expect(ids({ type: 'Private' })).toEqual(['late-party']);
    expect(ids({ division: 'CLM', type: 'Wedding' })).toEqual([]);
  });

  it('includes events on both end dates', () => {
    expect(ids({ from: '2026-12-05', to: '2026-12-05' })).toEqual(['wedding']);
    expect(ids({ from: '2026-12-06', to: '2026-12-10' })).toEqual([]);
  });

  it('counts an event spanning several days on any of those days', () => {
    expect(ids({ from: '2026-12-12', to: '2026-12-12' })).toEqual(['festival']);
    expect(ids({ from: '2026-12-13' })).toEqual(['festival', 'late-party']);
  });

  it('works out the last day in cape town time, not utc', () => {
    expect(ids({ from: '2026-12-20', to: '2026-12-20' })).toEqual(['late-party']);
  });
});
