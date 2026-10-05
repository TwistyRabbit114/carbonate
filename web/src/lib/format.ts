//----------------------------------------------------------\\
//                              MONEY
//----------------------------------------------------------\\

const zar = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' });

//"R 84 500,00", the en-ZA format. whole rands vs cents is still to confirm with the bookkeeper
export function formatMoney(value: number) {
  return zar.format(value);
}

//----------------------------------------------------------\\
//                              COUNTS
//----------------------------------------------------------\\

const count = new Intl.NumberFormat('en-ZA', { maximumFractionDigits: 0 });

//pack sizes and quantities, grouped the same way as money: "2 500"
export function formatCount(value: number) {
  return count.format(value);
}

//----------------------------------------------------------\\
//                              LISTS
//----------------------------------------------------------\\

const list = new Intl.ListFormat('en-ZA', { type: 'conjunction' });

//names in a sentence: "Thabo N. and Priya R."
export function formatList(items: readonly string[]) {
  return list.format(items);
}

//a full stop at the end, unless it already has one: "assigned to Sarah M." not "Sarah M.."
export function sentence(text: string) {
  return /[.!?]$/.test(text) ? text : `${text}.`;
}

//----------------------------------------------------------\\
//                              DATES
//----------------------------------------------------------\\

const timeZone = 'Africa/Johannesburg';

//en-ZA defaults to year-first (2026/11/28), so day, month and year are spelled out (NFR-32)
const dateFormat = new Intl.DateTimeFormat('en-ZA', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone,
});

const dateTimeFormat = new Intl.DateTimeFormat('en-ZA', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone,
});

//api sends UTC instants ending in Z, these render in SAST: "28 Nov 2026" and "28 Nov 2026, 09:00"
export const formatDate = (iso: string) => dateFormat.format(new Date(iso));
export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso));

//DateOnly values ("2026-11-28") are calendar dates, not instants, so pin them to midday UTC
//to stop them sliding a day either side of the timezone
export const formatDateOnly = (value: string) => dateFormat.format(new Date(`${value}T12:00:00Z`));

//----------------------------------------------------------\\
//                              CALENDAR DATES
//----------------------------------------------------------\\

//not for display: en-CA happens to give yyyy-mm-dd, which compares as plain text against
//DateOnly values and date inputs
const isoDateFormat = new Intl.DateTimeFormat('en-CA', { timeZone });

//the SAST calendar day a UTC instant falls on, "2026-11-28T23:30:00Z" is the 29th in Cape Town
export const sastCalendarDate = (iso: string) => isoDateFormat.format(new Date(iso));

const clockFormat = new Intl.DateTimeFormat('en-GB', {
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone,
});

//an instant as a datetime-local input wants it, in SAST: "2026-11-28T09:00"
export const sastDateTimeInput = (iso: string) =>
  `${sastCalendarDate(iso)}T${clockFormat.format(new Date(iso))}`;

//and back again. SAST is UTC+2 all year with no daylight saving, so the offset is fixed
export const fromSastDateTimeInput = (value: string) => new Date(`${value}:00+02:00`).toISOString();
