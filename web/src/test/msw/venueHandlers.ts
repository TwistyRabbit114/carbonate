import { delay, http, HttpResponse } from 'msw';
import type { SiteVisit, Venue } from '@/api/types';
import {
  caller,
  forbidden,
  invalid,
  mock,
  notFound,
  orNull,
  personRefFor,
  readJson,
  seesAll,
  trimmed,
  unauthorised,
  visibleEvent,
} from './mockCore';

//venues persist between events and are reused (FR-32), and each event keeps its recces (FR-33).
//the same checks and wording as the api's venue and site visit endpoints

//----------------------------------------------------------\\
//                              CHECKS
//----------------------------------------------------------\\

const time = (value: unknown) =>
  typeof value === 'string' && /^\d{2}:\d{2}(:\d{2})?$/.test(value) ? value : null;
const withSeconds = (value: string | null) => (value && value.length === 5 ? `${value}:00` : value);

function checkVenue(body: Record<string, unknown>): Omit<Venue, 'venueId'> | Response {
  const errors: Record<string, string[]> = {};
  const opens = withSeconds(time(body.operatingHoursStart));
  const closes = withSeconds(time(body.operatingHoursEnd));
  if (!trimmed(body.name)) errors.name = ["Enter the venue's name."];
  if (!trimmed(body.address)) errors.address = ["Enter the venue's address."];
  if (opens && !closes) errors.operatingHoursEnd = ['Add the closing time too, or leave both times empty.'];
  if (closes && !opens) errors.operatingHoursStart = ['Add the opening time too, or leave both times empty.'];
  if (Object.keys(errors).length > 0) return invalid(errors);

  return {
    name: trimmed(body.name),
    address: trimmed(body.address),
    accessRoute: orNull(trimmed(body.accessRoute)),
    loadingBayDetails: orNull(trimmed(body.loadingBayDetails)),
    operatingHoursStart: opens,
    operatingHoursEnd: closes,
    requiresSecurityClearance: body.requiresSecurityClearance === true,
    requiresHealthSafetyFile: body.requiresHealthSafetyFile === true,
    ppeRequirements: orNull(trimmed(body.ppeRequirements)),
    isActive: body.isActive !== false,
  };
}

type VisitFields = Omit<SiteVisit, 'siteVisitId' | 'eventId' | 'conductedBy'>;

function checkVisit(body: Record<string, unknown>): VisitFields | Response {
  const errors: Record<string, string[]> = {};
  const plate = trimmed(body.licencePlate);
  if (!trimmed(body.visitDate)) errors.visitDate = ['Enter the date of the visit.'];
  if (plate && !/^[A-Za-z0-9 -]+$/.test(plate)) {
    errors.licencePlate = ['Use letters, numbers, spaces and dashes only, like CA 123-456.'];
  }
  if (Object.keys(errors).length > 0) return invalid(errors);

  const text = (key: string) => orNull(trimmed(body[key]));
  return {
    visitDate: trimmed(body.visitDate),
    vehicleType: text('vehicleType'),
    licencePlate: plate ? plate.toUpperCase() : null,
    driverName: text('driverName'),
    requiredDriverDetails: text('requiredDriverDetails'),
    crewNames: text('crewNames'),
    signInProcedure: text('signInProcedure'),
    securityCheckpoint: text('securityCheckpoint'),
    healthSafetyFileRef: text('healthSafetyFileRef'),
    notes: text('notes'),
  };
}

//----------------------------------------------------------\\
//                              HANDLERS
//----------------------------------------------------------\\

export const venueHandlers = [
  http.get('/api/venues', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!seesAll(who)) return forbidden();

    const params = new URL(request.url).searchParams;
    const q = (params.get('q') ?? '').trim().toLowerCase();
    const includeInactive = params.get('includeInactive') === 'true';
    const items = mock.venues
      .filter((venue) => includeInactive || venue.isActive)
      .filter((venue) => venue.name.toLowerCase().includes(q) || venue.address.toLowerCase().includes(q));
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  //crew get a venue only through an event they're on (US-23)
  http.get('/api/venues/:venueId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const venue = mock.venues.find((candidate) => candidate.venueId === params.venueId);
    const reachable =
      venue &&
      (seesAll(who) ||
        mock.events.some((event) => event.venueId === venue.venueId && visibleEvent(who, event.eventId)));
    return reachable ? HttpResponse.json(venue) : notFound();
  }),

  http.post('/api/venues', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('venue.edit')) return forbidden();

    const checked = checkVenue(await readJson(request));
    if (checked instanceof Response) return checked;
    const venue: Venue = { ...checked, venueId: crypto.randomUUID() };
    mock.venues = [...mock.venues, venue];
    return HttpResponse.json(venue, { status: 201 });
  }),

  http.put('/api/venues/:venueId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('venue.edit')) return forbidden();
    const venue = mock.venues.find((candidate) => candidate.venueId === params.venueId);
    if (!venue) return notFound();

    const checked = checkVenue(await readJson(request));
    if (checked instanceof Response) return checked;
    const updated: Venue = { ...checked, venueId: venue.venueId };
    mock.venues = mock.venues.map((candidate) => (candidate.venueId === venue.venueId ? updated : candidate));
    return HttpResponse.json(updated);
  }),

  http.get('/api/events/:eventId/site-visits', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();
    return HttpResponse.json(mock.siteVisits.filter((visit) => visit.eventId === event.eventId));
  }),

  //a recce saved without a conductor is put down to whoever saves it
  http.post('/api/events/:eventId/site-visits', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('venue.edit')) return forbidden();
    const event = visibleEvent(who, params.eventId);
    if (!event) return notFound();

    const body = await readJson(request);
    const checked = checkVisit(body);
    if (checked instanceof Response) return checked;
    const conductedBy =
      typeof body.conductedByUserId === 'string' ? personRefFor(body.conductedByUserId) : who.me;
    if (!conductedBy) return invalid({ conductedByUserId: ['That person does not exist.'] });

    const visit: SiteVisit = {
      ...checked,
      siteVisitId: crypto.randomUUID(),
      eventId: event.eventId,
      conductedBy,
    };
    mock.siteVisits = [...mock.siteVisits, visit];
    return HttpResponse.json(visit, { status: 201 });
  }),

  http.put('/api/site-visits/:siteVisitId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('venue.edit')) return forbidden();
    const visit = mock.siteVisits.find((candidate) => candidate.siteVisitId === params.siteVisitId);
    if (!visit || !visibleEvent(who, visit.eventId)) return notFound();

    const body = await readJson(request);
    const checked = checkVisit(body);
    if (checked instanceof Response) return checked;
    const conductedBy =
      typeof body.conductedByUserId === 'string' ? personRefFor(body.conductedByUserId) : visit.conductedBy;
    if (!conductedBy) return invalid({ conductedByUserId: ['That person does not exist.'] });

    const updated: SiteVisit = { ...visit, ...checked, conductedBy };
    mock.siteVisits = mock.siteVisits.map((candidate) => (candidate === visit ? updated : candidate));
    return HttpResponse.json(updated);
  }),
];
