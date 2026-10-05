import type { EventListItem, SiteVisit, Venue } from '@/api/types';
import { personRef } from './adminTasks';

//the demo venues, the same ones the api's demo seed loads, and a recce for each event that has
//had one. visit dates are worked out from the event so they always sit a week before it

//----------------------------------------------------------\\
//                              VENUES
//----------------------------------------------------------\\

const venue = (number: number, details: Omit<Venue, 'venueId' | 'isActive'>): Venue => ({
  venueId: `5e000000-0000-0000-0000-00000000000${number}`,
  isActive: true,
  ...details,
});

export function demoVenues(): Venue[] {
  return [
    venue(1, {
      name: 'Steenberg Estate',
      address: 'Steenberg Road, Tokai, Cape Town',
      accessRoute: 'Service gate off Steenberg Road; no vehicles past the lawn.',
      loadingBayDetails: 'Gravel bay behind the cellar. No loading dock, tail lift needed.',
      operatingHoursStart: '07:00:00',
      operatingHoursEnd: '23:00:00',
      requiresSecurityClearance: false,
      requiresHealthSafetyFile: true,
      ppeRequirements: 'Closed shoes, hi-vis during load-in.',
    }),
    venue(2, {
      name: 'V&A Waterfront',
      address: 'Dock Road, V&A Waterfront, Cape Town',
      accessRoute: 'Loading via Dock Road service entrance. Permit required.',
      loadingBayDetails: 'Shared bay, 30-minute limit, strictly enforced.',
      operatingHoursStart: '06:00:00',
      operatingHoursEnd: '02:00:00',
      requiresSecurityClearance: true,
      requiresHealthSafetyFile: true,
      ppeRequirements: 'Hi-vis and closed shoes in the service corridors.',
    }),
    venue(3, {
      name: 'The Point Hotel',
      address: 'Beach Road, Sea Point, Cape Town',
      accessRoute: null,
      loadingBayDetails: 'Basement bay, 2.1 m height limit.',
      operatingHoursStart: '06:00:00',
      operatingHoursEnd: '01:00:00',
      requiresSecurityClearance: true,
      requiresHealthSafetyFile: false,
      ppeRequirements: null,
    }),
    venue(4, {
      name: 'Riverside Grounds',
      address: 'Liesbeek Parkway, Observatory, Cape Town',
      accessRoute: 'Field access off Liesbeek Parkway. Soft ground after rain.',
      loadingBayDetails: null,
      operatingHoursStart: null,
      operatingHoursEnd: null,
      requiresSecurityClearance: false,
      requiresHealthSafetyFile: true,
      ppeRequirements: 'Hi-vis at all times during build.',
    }),
    venue(5, {
      name: 'Steenberg Golf Club',
      address: 'Steenberg Road, Tokai, Cape Town',
      accessRoute: null,
      loadingBayDetails: null,
      operatingHoursStart: '06:00:00',
      operatingHoursEnd: '20:00:00',
      requiresSecurityClearance: false,
      requiresHealthSafetyFile: false,
      ppeRequirements: null,
    }),
  ];
}

//the venue an event's list row names
export const venueIdFor = (venueName: string | null | undefined) =>
  demoVenues().find((candidate) => candidate.name === venueName)?.venueId ?? null;

//----------------------------------------------------------\\
//                              SITE VISITS
//----------------------------------------------------------\\

//a recce a week out for the confirmed and live events. the enquiry hasn't had one yet
export function demoSiteVisits(event: EventListItem): SiteVisit[] {
  if (event.status === 'Enquired' || event.status === 'Cancelled') return [];
  const number = event.eventId.slice(-1);
  const visitDate = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg' }).format(
    new Date(new Date(event.startsAt).getTime() - 7 * 24 * 3_600_000),
  );

  return [
    {
      siteVisitId: `5a000000-0000-0000-0000-00000000000${number}`,
      eventId: event.eventId,
      conductedBy: personRef('crewLead'),
      visitDate,
      vehicleType: '4-ton truck',
      licencePlate: 'CA 123-456',
      driverName: 'Sipho M.',
      requiredDriverDetails: 'ID number and licence plate to security 48 hours ahead.',
      crewNames: 'Thabo N., Priya R.',
      signInProcedure: 'Sign in at the gatehouse with ID. Collect contractor bibs from security.',
      securityCheckpoint: 'Main gatehouse',
      healthSafetyFileRef: 'HS-2026-114',
      notes: 'Power points behind the cellar, extension leads needed for the far bar.',
    },
  ];
}
