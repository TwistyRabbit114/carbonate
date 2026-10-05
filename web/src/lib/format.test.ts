import { describe, expect, it } from 'vitest';
import { formatCount, formatDate, formatDateOnly, formatDateTime, formatMoney } from './format';

//en-ZA groups thousands with a non-breaking space, swap it so the expectations stay readable
const plain = (value: string) => value.replace(/\u00a0/g, ' ');

//----------------------------------------------------------\\
//                              MONEY
//----------------------------------------------------------\\

describe('formatMoney', () => {
  it('formats rand amounts the en-ZA way', () => {
    expect(plain(formatMoney(84500))).toBe('R 84 500,00');
  });

  it('still shows a real zero rather than treating it as missing', () => {
    expect(plain(formatMoney(0))).toBe('R 0,00');
  });
});

describe('formatCount', () => {
  it('groups thousands the same way as money', () => {
    expect(plain(formatCount(2500))).toBe('2 500');
    expect(formatCount(90)).toBe('90');
  });
});

//----------------------------------------------------------\\
//                              DATES
//----------------------------------------------------------\\

describe('formatDate', () => {
  it('renders day-month-year in SAST', () => {
    expect(formatDate('2026-11-28T07:00:00Z')).toBe('28 Nov 2026');
  });

  it('rolls into the next day for late UTC times', () => {
    expect(formatDate('2026-11-28T23:30:00Z')).toBe('29 Nov 2026');
  });
});

describe('formatDateTime', () => {
  it('shows SAST time on a 24 hour clock', () => {
    expect(formatDateTime('2026-11-28T07:00:00Z')).toBe('28 Nov 2026, 09:00');
  });
});

describe('formatDateOnly', () => {
  it("doesn't shift a calendar date by a day", () => {
    expect(formatDateOnly('2026-11-28')).toBe('28 Nov 2026');
  });
});
