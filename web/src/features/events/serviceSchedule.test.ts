import { describe, expect, it } from 'vitest';
import { readServiceSchedule } from './serviceSchedule';

const schedule = {
  timezone: 'Africa/Johannesburg',
  doorsOpen: '15:30',
  dinnerServiceAt: '19:00',
  serviceWindows: [
    { label: 'Before dinner', start: '18:30', end: '19:00', expectedLoad: 'peak', barsOpen: 3 },
    { label: 'Dinner', start: '19:00', end: '20:30', expectedLoad: 'table-service' },
  ],
};

describe('readServiceSchedule', () => {
  it('reads a schedule in the schema shape', () => {
    const read = readServiceSchedule(schedule);
    expect(read?.doorsOpen).toBe('15:30');
    expect(read?.dinnerServiceAt).toBe('19:00');
    expect(read?.serviceWindows.map((window) => window.expectedLoad)).toEqual(['peak', 'table-service']);
    expect(read?.serviceWindows[0]?.barsOpen).toBe(3);
  });

  it('treats no schedule as missing', () => {
    expect(readServiceSchedule(null)).toBeNull();
    expect(readServiceSchedule(undefined)).toBeNull();
  });

  it("treats a schedule that doesn't fit the schema as missing rather than showing part of it", () => {
    expect(readServiceSchedule({ ...schedule, doorsOpen: 'half past three' })).toBeNull();
    expect(readServiceSchedule({ ...schedule, serviceWindows: [] })).toBeNull();
    expect(
      readServiceSchedule({
        ...schedule,
        serviceWindows: [{ label: 'Dinner', start: '19:00', end: '20:30', expectedLoad: 'rammed' }],
      }),
    ).toBeNull();
  });

  it('drops counts that make no sense instead of showing them', () => {
    const read = readServiceSchedule({
      ...schedule,
      serviceWindows: [{ label: 'Late', start: '22:00', end: '23:00', expectedLoad: 'low', barsOpen: -1 }],
    });
    expect(read?.serviceWindows[0]?.barsOpen).toBeUndefined();
  });
});
