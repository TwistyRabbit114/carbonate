import { describe, expect, it } from 'vitest';
import {
  addDays,
  addMonths,
  isCalendarDate,
  monthTitle,
  monthWeeks,
  startOfWeek,
  weekOf,
  weekTitle,
  weekdays,
} from './calendarDates';

describe('calendar dates', () => {
  it('starts weeks on a monday', () => {
    expect(startOfWeek('2026-10-09')).toBe('2026-10-05'); //a friday
    expect(startOfWeek('2026-10-05')).toBe('2026-10-05'); //already a monday
    expect(startOfWeek('2026-10-11')).toBe('2026-10-05'); //sunday ends the week
    expect(weekdays.at(0)).toEqual({ name: 'Monday', short: 'Mon' });
    expect(weekdays.at(-1)?.short).toBe('Sun');
  });

  it('covers a month in whole weeks', () => {
    const october = monthWeeks('2026-10-20');
    expect(october[0]?.[0]).toBe('2026-09-28');
    expect(october.at(-1)!.at(-1)).toBe('2026-11-01');
    expect(october).toHaveLength(5);
    expect(october.every((week) => week.length === 7)).toBe(true);
    //february 2027 starts on a monday and ends on a sunday, so it's exactly four weeks
    expect(monthWeeks('2027-02-10')).toHaveLength(4);
  });

  it('moves across month and year ends, and leap days', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2028-02-28', 1)).toBe('2028-02-29');
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-01');
    expect(addMonths('2026-01-15', -1)).toBe('2025-12-01');
    expect(weekOf('2026-12-31')).toEqual([
      '2026-12-28',
      '2026-12-29',
      '2026-12-30',
      '2026-12-31',
      '2027-01-01',
      '2027-01-02',
      '2027-01-03',
    ]);
  });

  it('only accepts real days from the address bar', () => {
    expect(isCalendarDate('2026-10-09')).toBe(true);
    expect(isCalendarDate('2026-02-31')).toBe(false);
    expect(isCalendarDate('9 Oct')).toBe(false);
    expect(isCalendarDate(null)).toBe(false);
  });

  it('names months and weeks the way people say them', () => {
    expect(monthTitle('2026-10-09')).toBe('October 2026');
    expect(weekTitle('2026-10-09')).toBe('5 to 11 Oct 2026');
    expect(weekTitle('2026-10-01')).toBe('28 Sept to 4 Oct 2026'); //en-ZA's abbreviation, as on every other date in the app
    expect(weekTitle('2026-12-31')).toBe('28 Dec 2026 to 3 Jan 2027');
  });
});
