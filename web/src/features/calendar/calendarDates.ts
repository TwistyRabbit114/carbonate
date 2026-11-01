import { sastCalendarDate } from '@/lib/format';

//calendar days are plain "yyyy-mm-dd" strings. the sums run in utc so a day is always 24 hours,
//and SAST has no daylight saving, so nothing here can slip a day

const day = 86_400_000;
const toTime = (date: string) => Date.parse(`${date}T00:00:00Z`);
const fromTime = (time: number) => new Date(time).toISOString().slice(0, 10);

//----------------------------------------------------------\\
//                              DAYS
//----------------------------------------------------------\\

export const todayInSast = () => sastCalendarDate(new Date().toISOString());

export const addDays = (date: string, days: number) => fromTime(toTime(date) + days * day);

//a real day in yyyy-mm-dd, so a hand-typed ?date=2026-02-31 falls back to today
export function isCalendarDate(value: string | null): value is string {
  return !!value && /^\d{4}-\d{2}-\d{2}$/.test(value) && fromTime(toTime(value)) === value;
}

//monday is 0, weeks start on a monday here
const weekday = (date: string) => (new Date(toTime(date)).getUTCDay() + 6) % 7;

export const startOfWeek = (date: string) => addDays(date, -weekday(date));
export const startOfMonth = (date: string) => `${date.slice(0, 7)}-01`;
export const sameMonth = (a: string, b: string) => a.slice(0, 7) === b.slice(0, 7);

//the first of the month n months away
export function addMonths(date: string, months: number) {
  const first = new Date(toTime(startOfMonth(date)));
  first.setUTCMonth(first.getUTCMonth() + months);
  return fromTime(first.getTime());
}

//----------------------------------------------------------\\
//                              GRIDS
//----------------------------------------------------------\\

//the seven days of the week a date is in
export const weekOf = (date: string) => Array.from({ length: 7 }, (_, i) => addDays(startOfWeek(date), i));

//whole weeks covering the month, so the grid starts on a monday and ends on a sunday
export function monthWeeks(date: string): string[][] {
  const lastDay = addDays(addMonths(date, 1), -1);
  const weeks: string[][] = [];
  for (let start = startOfWeek(startOfMonth(date)); start <= lastDay; start = addDays(start, 7)) {
    weeks.push(weekOf(start));
  }
  return weeks;
}

//----------------------------------------------------------\\
//                              LABELS
//----------------------------------------------------------\\

//pinned to midday utc and shown in utc, the same trick as formatDateOnly, so the label is the day
const at = (date: string) => new Date(`${date}T12:00:00Z`);
const monthFormat = new Intl.DateTimeFormat('en-ZA', { month: 'long', year: 'numeric', timeZone: 'UTC' });
const longMonth = new Intl.DateTimeFormat('en-ZA', { month: 'long', timeZone: 'UTC' });
const shortMonth = new Intl.DateTimeFormat('en-ZA', { month: 'short', timeZone: 'UTC' });
const weekdayName = new Intl.DateTimeFormat('en-ZA', { weekday: 'long', timeZone: 'UTC' });

//"October 2026"
export const monthTitle = (date: string) => monthFormat.format(at(date));

//the day of the month without a leading zero, for the grid
export const dayNumber = (date: string) => String(Number(date.slice(8)));

//"Friday 9 October". put together by hand because en-ZA pads the day to "09"
export const longDay = (date: string) =>
  `${weekdayName.format(at(date))} ${dayNumber(date)} ${longMonth.format(at(date))}`;

//"5 to 11 Oct 2026", or "28 Sep to 4 Oct 2026" when the week spans two months
export function weekTitle(date: string) {
  const [first, last] = [startOfWeek(date), addDays(startOfWeek(date), 6)];
  const month = (d: string) => shortMonth.format(at(d)).replace('.', '');
  const start = sameMonth(first, last) ? dayNumber(first) : `${dayNumber(first)} ${month(first)}`;
  const year = first.slice(0, 4) === last.slice(0, 4) ? '' : ` ${first.slice(0, 4)}`;
  return `${start}${year} to ${dayNumber(last)} ${month(last)} ${last.slice(0, 4)}`;
}

//monday to sunday, short for the column heads and in full for screen readers
export const weekdays = weekOf('2026-01-05').map((date) => ({
  name: weekdayName.format(at(date)),
  short: weekdayName.format(at(date)).slice(0, 3),
}));
