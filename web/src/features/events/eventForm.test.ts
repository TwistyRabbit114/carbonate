import { describe, expect, it } from 'vitest';
import { demoEventDetail } from '@/test/fixtures/eventDetails';
import {
  blankEvent,
  eventSchema,
  requestFromValues,
  valuesFromEvent,
  type EventFormValues,
} from './eventForm';

const filled: EventFormValues = {
  ...blankEvent,
  name: 'Harbour Launch',
  eventCode: 'HRB-LNCH-26',
  clientId: 'c1000000-0000-0000-0000-000000000002',
  venueId: '5e000000-0000-0000-0000-000000000002',
  divisionId: 'd1000000-0000-0000-0000-0000000000ce',
  eventType: 'Activation',
  eventDate: '2026-12-04',
  startsAt: '2026-12-04T10:00',
  endsAt: '2026-12-04T18:00',
  packSizeEstimated: '400',
  staffRequired: '8',
  budget: '120 000',
};

const errorsFor = (values: EventFormValues) => {
  const result = eventSchema.safeParse(values);
  return result.success
    ? {}
    : Object.fromEntries(result.error.issues.map((issue) => [issue.path[0], issue.message]));
};

describe('eventSchema', () => {
  it('takes a complete booking', () => {
    expect(errorsFor(filled)).toEqual({});
  });

  it('asks for every required field on an empty form', () => {
    expect(Object.keys(errorsFor(blankEvent)).sort()).toEqual(
      [
        'clientId',
        'divisionId',
        'endsAt',
        'eventCode',
        'eventDate',
        'name',
        'packSizeEstimated',
        'staffRequired',
        'startsAt',
        'venueId',
      ].sort(),
    );
  });

  it('holds the event code to the same allowlist as the api', () => {
    expect(errorsFor({ ...filled, eventCode: 'NAI WED 26' }).eventCode).toBeDefined();
    expect(errorsFor({ ...filled, eventCode: 'NAI' }).eventCode).toBeDefined();
  });

  it("won't take an end before the start", () => {
    expect(errorsFor({ ...filled, endsAt: '2026-12-04T09:00' }).endsAt).toBe(
      'The event must end after it starts.',
    );
  });

  it('wants whole numbers for counts', () => {
    expect(errorsFor({ ...filled, packSizeEstimated: '250.5' }).packSizeEstimated).toBeDefined();
    expect(errorsFor({ ...filled, staffRequired: '2000' }).staffRequired).toBeDefined();
  });
});

describe('requestFromValues', () => {
  it('sends SAST times as UTC instants and counts as numbers', () => {
    const request = requestFromValues(filled, { canSetBudget: true });
    expect(request.startsAt).toBe('2026-12-04T08:00:00.000Z');
    expect(request.endsAt).toBe('2026-12-04T16:00:00.000Z');
    expect(request.packSizeEstimated).toBe(400);
    expect(request.headcountExpected).toBeNull();
    expect(request.budgetAmount).toBe(120000);
  });

  it("never sends a budget for someone who can't see it", () => {
    expect(requestFromValues(filled, { canSetBudget: false }).budgetAmount).toBeNull();
  });

  it('sends untouched times back exactly as the api had them', () => {
    const base = {
      ...demoEventDetail('0e000000-0000-0000-0000-000000000002')!,
      startsAt: '2026-12-04T08:00:30Z',
    };
    const request = requestFromValues(valuesFromEvent(base), { canSetBudget: false, base });
    expect(request.startsAt).toBe('2026-12-04T08:00:30Z');
  });

  it('round-trips an event through the form unchanged', () => {
    const base = { ...demoEventDetail('0e000000-0000-0000-0000-000000000001')!, budgetAmount: 90_000 };
    const request = requestFromValues(valuesFromEvent(base), { canSetBudget: true, base });
    expect(request).toMatchObject({
      name: base.name,
      eventCode: base.eventCode,
      clientId: base.clientId,
      venueId: base.venueId,
      startsAt: base.startsAt,
      endsAt: base.endsAt,
      isConfidential: true,
      budgetAmount: 90_000,
    });
  });
});
