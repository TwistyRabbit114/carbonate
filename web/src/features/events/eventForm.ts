import { z } from 'zod';
import type { EventDetail, EventType, SaveEventRequest } from '@/api/types';
import { fromSastDateTimeInput, parseRand, sastDateTimeInput } from '@/lib/format';

//the booking form (FR-01): its rules, which match the api's validator so most mistakes are caught
//before a round trip, and the mapping between what the inputs hold and what the api takes

//----------------------------------------------------------\\
//                              RULES
//----------------------------------------------------------\\

export const eventTypes = [
  'Activation',
  'Corporate',
  'Wedding',
  'Festival',
  'YearEnd',
  'Private',
] as const satisfies readonly EventType[];

//a number input's text as a whole number in a range, or empty when optional
const wholeNumber = (label: string, max: number, required: boolean) =>
  z
    .string()
    .trim()
    .refine((value) => !required || value !== '', `Enter the ${label}.`)
    .refine(
      (value) => value === '' || (/^\d+$/.test(value) && Number(value) <= max),
      `Enter the ${label} as a whole number, up to ${max.toLocaleString('en-ZA')}.`,
    );

export const eventSchema = z
  .object({
    name: z.string().trim().min(1, 'Give the event a name.').max(200, 'Keep the name under 200 characters.'),
    eventCode: z
      .string()
      .trim()
      .min(1, 'Give the event a code.')
      .regex(/^[A-Za-z0-9-]{4,20}$/, 'That code has to be 4 to 20 letters, numbers and dashes.'),
    clientId: z.string().min(1, 'Choose the client.'),
    venueId: z.string().min(1, 'Choose a venue.'),
    divisionId: z.string().min(1, 'Choose the division.'),
    eventType: z.enum(eventTypes),
    eventDate: z.string().min(1, 'Choose the event date.'),
    startsAt: z.string().min(1, 'Choose when the event starts.'),
    endsAt: z.string().min(1, 'Choose when the event ends.'),
    packSizeEstimated: wholeNumber('expected pack size', 100_000, true),
    headcountExpected: wholeNumber('expected headcount', 100_000, false),
    staffRequired: wholeNumber('number of staff', 1_000, true),
    paymentMode: z.enum(['PurchaseOrder', 'Deposit']),
    infrastructureMode: z.enum(['Owned', 'Rented']),
    confidential: z.enum(['no', 'yes']),
    budget: z
      .string()
      .trim()
      .refine((value) => value === '' || parseRand(value) !== null, 'Enter the budget in rand, like 90 000.'),
  })
  .refine((values) => !values.startsAt || !values.endsAt || values.endsAt > values.startsAt, {
    path: ['endsAt'],
    message: 'The event must end after it starts.',
  });

export type EventFormValues = z.infer<typeof eventSchema>;

//the api's field names mapped onto the form's, so its errors land against the right field
export const formFieldFor: Record<string, keyof EventFormValues> = {
  name: 'name',
  eventCode: 'eventCode',
  clientId: 'clientId',
  venueId: 'venueId',
  divisionId: 'divisionId',
  eventType: 'eventType',
  eventDate: 'eventDate',
  startsAt: 'startsAt',
  endsAt: 'endsAt',
  packSizeEstimated: 'packSizeEstimated',
  headcountExpected: 'headcountExpected',
  staffRequired: 'staffRequired',
  paymentMode: 'paymentMode',
  infrastructureMode: 'infrastructureMode',
  isConfidential: 'confidential',
  budgetAmount: 'budget',
};

//----------------------------------------------------------\\
//                              MAPPING
//----------------------------------------------------------\\

export const blankEvent: EventFormValues = {
  name: '',
  eventCode: '',
  clientId: '',
  venueId: '',
  divisionId: '',
  eventType: 'Corporate',
  eventDate: '',
  startsAt: '',
  endsAt: '',
  packSizeEstimated: '',
  headcountExpected: '',
  staffRequired: '',
  paymentMode: 'PurchaseOrder',
  infrastructureMode: 'Owned',
  confidential: 'no',
  budget: '',
};

//an amount back into the box the way it was typed, without the currency formatting
const budgetText = (amount: number | null | undefined) => (amount == null ? '' : String(amount));

export function valuesFromEvent(event: EventDetail): EventFormValues {
  return {
    name: event.name,
    eventCode: event.eventCode,
    clientId: event.clientId,
    venueId: event.venueId ?? '',
    divisionId: event.divisionId,
    eventType: event.eventType,
    eventDate: event.eventDate,
    startsAt: sastDateTimeInput(event.startsAt),
    endsAt: sastDateTimeInput(event.endsAt),
    packSizeEstimated: String(event.packSizeEstimated),
    headcountExpected: event.headcountExpected == null ? '' : String(event.headcountExpected),
    staffRequired: String(event.staffRequired),
    paymentMode: event.paymentMode,
    infrastructureMode: event.infrastructureMode,
    confidential: event.isConfidential ? 'yes' : 'no',
    budget: budgetText(event.budgetAmount),
  };
}

//times go back exactly as the api sent them when nobody touched them, so an untouched form never
//nudges a start time by the seconds the inputs can't show
export function requestFromValues(
  values: EventFormValues,
  options: { canSetBudget: boolean; base?: EventDetail },
): SaveEventRequest {
  const { canSetBudget, base } = options;
  const start = base ? valuesFromEvent(base) : undefined;
  const instant = (value: string, key: 'startsAt' | 'endsAt') =>
    base && start?.[key] === value ? base[key] : fromSastDateTimeInput(value);

  return {
    name: values.name.trim(),
    eventCode: values.eventCode.trim(),
    clientId: values.clientId,
    venueId: values.venueId,
    divisionId: values.divisionId,
    eventType: values.eventType,
    eventDate: values.eventDate,
    startsAt: instant(values.startsAt, 'startsAt'),
    endsAt: instant(values.endsAt, 'endsAt'),
    packSizeEstimated: Number(values.packSizeEstimated),
    headcountExpected: values.headcountExpected === '' ? null : Number(values.headcountExpected),
    staffRequired: Number(values.staffRequired),
    paymentMode: values.paymentMode,
    infrastructureMode: values.infrastructureMode,
    isConfidential: values.confidential === 'yes',
    //someone who can't see the budget sends nothing for it, and the api leaves it as it was
    budgetAmount: canSetBudget && values.budget !== '' ? parseRand(values.budget) : null,
  };
}
