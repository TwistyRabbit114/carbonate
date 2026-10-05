import type { EventListItem } from '@/api/types';

//the demo events from plan appendix d and the prototype. dates are worked out when this is
//called, relative to now, so each event's stage always matches its dates (live means live)

//----------------------------------------------------------\\
//                              DATES
//----------------------------------------------------------\\

const hour = 3_600_000;
const day = 24 * hour;

//calendar date as Cape Town sees it, en-CA happens to format as yyyy-mm-dd
const sastDate = (instant: number) =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg' }).format(new Date(instant));

//n days from today, between two SAST clock times. an end before the start rolls into the next day
function scheduled(daysFromNow: number, start: string, end: string) {
  const eventDate = sastDate(Date.now() + daysFromNow * day);
  const startsAt = new Date(`${eventDate}T${start}:00+02:00`).getTime();
  let endsAt = new Date(`${eventDate}T${end}:00+02:00`).getTime();
  if (endsAt <= startsAt) endsAt += day;
  return { eventDate, startsAt: new Date(startsAt).toISOString(), endsAt: new Date(endsAt).toISOString() };
}

//started two hours ago and finishes in six
function liveNow() {
  const startsAt = Date.now() - 2 * hour;
  return {
    eventDate: sastDate(startsAt),
    startsAt: new Date(startsAt).toISOString(),
    endsAt: new Date(startsAt + 8 * hour).toISOString(),
  };
}

//----------------------------------------------------------\\
//                              EVENTS
//----------------------------------------------------------\\

export function demoEvents(): EventListItem[] {
  return [
    {
      eventId: '0e000000-0000-0000-0000-000000000001',
      eventCode: 'NAI-WED-26',
      name: 'Naidoo Wedding',
      status: 'ConfirmedInPlanning',
      eventType: 'Wedding',
      divisionCode: 'CE',
      ...scheduled(21, '15:30', '00:30'),
      venueName: 'Steenberg Estate',
      packSizeEstimated: 180,
      isConfidential: true,
      rowVersion: 'AAAAAAAAB9E=',
    },
    {
      eventId: '0e000000-0000-0000-0000-000000000002',
      eventCode: 'VAN-ACT-26',
      name: 'Vantage Brand Activation',
      status: 'ConfirmedInPlanning',
      eventType: 'Activation',
      divisionCode: 'CE',
      ...scheduled(4, '10:00', '18:00'),
      venueName: 'V&A Waterfront',
      packSizeEstimated: 600,
      isConfidential: false,
      rowVersion: 'AAAAAAAAB9I=',
    },
    {
      eventId: '0e000000-0000-0000-0000-000000000003',
      eventCode: 'MER-YE-26',
      name: 'Meridian Year-End Function',
      status: 'ConfirmedInPlanning',
      eventType: 'Corporate',
      divisionCode: 'CE',
      ...scheduled(42, '18:00', '23:30'),
      venueName: 'The Point Hotel',
      packSizeEstimated: 240,
      isConfidential: false,
      rowVersion: 'AAAAAAAAB9M=',
    },
    {
      eventId: '0e000000-0000-0000-0000-000000000004',
      eventCode: 'RIV-FEST-26',
      name: 'Riverlight Festival',
      status: 'InProgress',
      eventType: 'Festival',
      divisionCode: 'CLM',
      ...liveNow(),
      venueName: 'Riverside Grounds',
      packSizeEstimated: 2500,
      isConfidential: false,
      rowVersion: 'AAAAAAAAB9Q=',
    },
    {
      eventId: '0e000000-0000-0000-0000-000000000005',
      eventCode: 'DEL-GOLF-26',
      name: 'Delacroix Corporate Golf Day',
      status: 'Finished',
      eventType: 'Corporate',
      divisionCode: 'CE',
      ...scheduled(-14, '08:00', '17:00'),
      venueName: 'Steenberg Golf Club',
      packSizeEstimated: 90,
      packSizeActual: 86,
      isConfidential: false,
      rowVersion: 'AAAAAAAAB9U=',
    },
    //an enquiry with no po or deposit yet, it must never reach the board (FR-03)
    {
      eventId: '0e000000-0000-0000-0000-000000000006',
      eventCode: 'ATL-LNCH-26',
      name: 'Atlas Product Launch',
      status: 'Enquired',
      eventType: 'Corporate',
      divisionCode: 'CE',
      ...scheduled(56, '18:00', '22:00'),
      venueName: 'The Point Hotel',
      packSizeEstimated: 120,
      isConfidential: false,
      rowVersion: 'AAAAAAAAB9Y=',
    },
  ];
}
