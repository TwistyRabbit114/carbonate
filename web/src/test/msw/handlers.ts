import { delay, http, HttpResponse } from 'msw';
import type {
  Board,
  BoardColumn,
  CardStatus,
  CrewAssignment,
  EventDetail,
  EventListItem,
  EventStatus,
  Milestone,
  TaskCard,
  UserRef,
} from '@/api/types';
import { demoClients, demoDivisions } from '../fixtures/commercial';
import { demoEventBoard } from '../fixtures/eventBoards';
import { demoMilestones } from '../fixtures/eventDetails';
import { mfaStep, signedInSession } from '../fixtures/sessions';
import { users } from '../fixtures/users';
import { financeHandlers } from './financeHandlers';
import {
  accessTokenFor,
  accountFromBearer,
  accountFromMfaBearer,
  accountFromMfaToken,
  caller,
  canOpenEvent,
  canSeeEvent,
  eventFor,
  invalid,
  mfaTokenFor,
  mock,
  nextRowVersion,
  orNull,
  personRefFor,
  problem,
  readJson,
  seesAll,
  trimmed,
  unauthorised,
  visibleEvent,
  without,
  type AccountKey,
  type Caller,
} from './mockCore';
import { settingsHandlers } from './settingsHandlers';
import { stockHandlers } from './stockHandlers';
import { venueHandlers } from './venueHandlers';

//a stand-in for the api so screens can be built and demoed before the real endpoints exist.
//loaded by `npm run dev:mocks` only, and never part of the production build. the shared data and
//helpers are in mockCore, and each area beyond events and cards has a file of its own

export { accessTokenFor, resetMocks, type AccountKey } from './mockCore';

//----------------------------------------------------------\\
//                              DEMO ACCOUNTS
//----------------------------------------------------------\\

export const demoPassword = 'demo';
export const demoMfaCode = '123456';

const accountByEmail = new Map(
  (Object.keys(users) as AccountKey[]).map((key) => [users[key].user.email.toLowerCase(), key]),
);

//director shows the everyday code step, the bookkeeper shows first-time setup
const mfaAccounts: Partial<Record<AccountKey, 'verify' | 'enrol'>> = {
  director: 'verify',
  accounts: 'enrol',
};

//the api's wording, so the screens are tried against the same answers they'll really get
const wrongPassword = () =>
  problem(
    401,
    'Sign in required.',
    'That email and password do not match, or the account is temporarily locked.',
  );
const wrongCode = () =>
  problem(401, 'Sign in required.', 'That code is not right. Check the app and try again.');

//an expired or missing bearer is turned away before the endpoint runs, with no body
const noBearer = () => new HttpResponse(null, { status: 401 });

function signedIn(key: AccountKey) {
  mock.session = key;
  return HttpResponse.json(signedInSession(accessTokenFor(key)));
}

//----------------------------------------------------------\\
//                              AUTH
//----------------------------------------------------------\\

export const authHandlers = [
  http.post('/api/auth/login', async ({ request }) => {
    await delay();
    const { email, password } = await readJson(request);
    const key = typeof email === 'string' ? accountByEmail.get(email.toLowerCase()) : undefined;
    if (!key || password !== demoPassword) return wrongPassword();

    const step = mfaAccounts[key];
    if (!step) return signedIn(key);
    return HttpResponse.json(mfaStep(mfaTokenFor(key), step === 'enrol'));
  }),

  http.post('/api/auth/mfa/verify', async ({ request }) => {
    await delay();
    const { mfaToken, code } = await readJson(request);
    const key = accountFromMfaToken(mfaToken);
    if (!key) return problem(401, 'Sign in required.', 'Your sign-in timed out. Please start again.');
    return code === demoMfaCode ? signedIn(key) : wrongCode();
  }),

  http.post('/api/auth/mfa/enrol', async ({ request }) => {
    await delay();
    const key = accountFromMfaBearer(request.headers.get('Authorization'));
    if (!key) return noBearer();

    const label = encodeURIComponent(`Carbonate:${users[key].user.email}`);
    return HttpResponse.json({
      otpauthUri: `otpauth://totp/${label}?secret=JBSWY3DPEHPK3PXP&issuer=Carbonate`,
    });
  }),

  http.post('/api/auth/mfa/confirm', async ({ request }) => {
    await delay();
    const key = accountFromMfaBearer(request.headers.get('Authorization'));
    if (!key) return noBearer();
    return (await readJson(request)).code === demoMfaCode ? signedIn(key) : wrongCode();
  }),

  http.post('/api/auth/refresh', () => {
    if (!mock.session) return new HttpResponse(null, { status: 401 });
    return HttpResponse.json(signedInSession(accessTokenFor(mock.session)));
  }),

  http.post('/api/auth/logout', () => {
    mock.session = null;
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/me', ({ request }) => {
    const key = accountFromBearer(request.headers.get('Authorization'));
    return key ? HttpResponse.json(users[key]) : new HttpResponse(null, { status: 401 });
  }),
];

//----------------------------------------------------------\\
//                              EVENT HELPERS
//----------------------------------------------------------\\

const listItemOf = (event: EventDetail): EventListItem => ({
  eventId: event.eventId,
  eventCode: event.eventCode,
  name: event.name,
  status: event.status,
  eventType: event.eventType,
  divisionCode: event.divisionCode,
  eventDate: event.eventDate,
  startsAt: event.startsAt,
  endsAt: event.endsAt,
  venueName: event.venueName,
  packSizeEstimated: event.packSizeEstimated,
  packSizeActual: event.packSizeActual,
  isConfidential: event.isConfidential,
  rowVersion: event.rowVersion,
});

const staleEvent = (who: Caller, event: EventDetail) =>
  problem(
    409,
    'Someone else changed this event.',
    'Reload the event to see their changes, then make yours again.',
    {
      type: '/problems/concurrency-conflict',
      current: eventFor(who, event),
    },
  );

//----------------------------------------------------------\\
//                              EVENTS
//----------------------------------------------------------\\

export const eventHandlers = [
  http.get('/api/events', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const visible = mock.events.filter((event) => event.isActive && canSeeEvent(who, event.eventId));
    const boardOnly = new URL(request.url).searchParams.get('board') === 'true';
    const items = (
      boardOnly
        ? visible.filter((event) => event.status !== 'Enquired' && event.status !== 'Cancelled')
        : visible
    ).map(listItemOf);

    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.get('/api/events/:eventId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    return event ? HttpResponse.json(eventFor(who, event)) : problem(404, 'Not found');
  }),

  http.post('/api/events', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.create')) return problem(403, 'Forbidden');

    const body = await readJson(request);
    const checked = checkEvent(who, body, null);
    if (checked instanceof Response) return checked;

    const event: EventDetail = {
      ...checked,
      eventId: crypto.randomUUID(),
      status: 'Enquired',
      createdByUserId: who.me.userId,
      serviceSchedule: null,
      createdAt: new Date().toISOString(),
      isActive: true,
      packSizeActual: null,
      headcountConfirmed: null,
      rowVersion: nextRowVersion(),
    };
    mock.events = [...mock.events, event];
    mock.milestones = [...mock.milestones, ...demoMilestones(event)];
    mock.eventBoards = [...mock.eventBoards, demoEventBoard(event)];
    return HttpResponse.json(eventFor(who, event), { status: 201 });
  }),

  http.put('/api/events/:eventId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.edit')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    const body = await readJson(request);
    const checked = checkEvent(who, body, event.eventId);
    if (checked instanceof Response) return checked;
    if (body.rowVersion !== event.rowVersion) return staleEvent(who, event);

    //someone who can't see the budget can neither change nor erase it
    const budgetAmount = who.can('finance.view_client_price') ? checked.budgetAmount : event.budgetAmount;
    const updated = { ...event, ...checked, budgetAmount, rowVersion: nextRowVersion() };
    mock.events = mock.events.map((candidate) => (candidate.eventId === event.eventId ? updated : candidate));
    return HttpResponse.json(eventFor(who, updated));
  }),

  http.delete('/api/events/:eventId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.delete')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    mock.events = mock.events.map((candidate) =>
      candidate.eventId === event.eventId ? { ...candidate, isActive: false } : candidate,
    );
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/events/:eventId/milestones', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    return HttpResponse.json(mock.milestones.filter((milestone) => milestone.eventId === event.eventId));
  }),

  //the chain is one line, recce to reconciliation, so moving one moves everything after it by the
  //same amount (FR-04). the api's calculator handles any shape, this is enough for the screens
  http.post('/api/events/:eventId/milestones/:milestoneId/reschedule', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.edit')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    const chain = mock.milestones.filter((milestone) => milestone.eventId === event.eventId);
    const index = chain.findIndex((milestone) => milestone.milestoneId === params.milestoneId);
    const target = chain[index];
    if (!target) return problem(404, 'Not found', 'That milestone was not found on this event.');

    const body = await readJson(request);
    const newStart = typeof body.newStart === 'string' ? new Date(body.newStart).getTime() : NaN;
    const newEnd = typeof body.newEnd === 'string' ? new Date(body.newEnd).getTime() : NaN;
    if (Number.isNaN(newStart) || Number.isNaN(newEnd))
      return invalid({ newStart: ['Choose the new start.'] });
    if (newEnd < newStart) return invalid({ newEnd: ['A milestone cannot end before it starts.'] });
    if (body.rowVersion !== event.rowVersion) return staleEvent(who, event);

    const shift = newStart - new Date(target.scheduledStart).getTime();
    const moving = chain.slice(index);
    if (moving.some((milestone) => milestone.actualStart)) {
      return problem(422, 'Business rule', 'This milestone, or one that follows it, has already happened.');
    }

    const by = (iso: string) => new Date(new Date(iso).getTime() + shift).toISOString();
    const moved: Milestone[] = moving.map((milestone) =>
      milestone === target
        ? {
            ...milestone,
            scheduledStart: new Date(newStart).toISOString(),
            scheduledEnd: new Date(newEnd).toISOString(),
          }
        : {
            ...milestone,
            scheduledStart: by(milestone.scheduledStart),
            scheduledEnd: by(milestone.scheduledEnd),
          },
    );
    mock.milestones = mock.milestones.map(
      (milestone) => moved.find((change) => change.milestoneId === milestone.milestoneId) ?? milestone,
    );
    const rowVersion = nextRowVersion();
    mock.events = mock.events.map((candidate) =>
      candidate.eventId === event.eventId ? { ...candidate, rowVersion } : candidate,
    );
    return HttpResponse.json({ milestones: moved, rowVersion });
  }),

  //hourly rates are $staff: director and accounts see everyone's, anyone else only their own
  http.get('/api/events/:eventId/crew', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    return HttpResponse.json(
      mock.crew.filter((shift) => shift.eventId === event.eventId).map((shift) => shiftFor(who, shift)),
    );
  }),

  http.post('/api/events/:eventId/crew', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('crew.assign')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    const body = await readJson(request);
    const person = typeof body.userId === 'string' ? personRefFor(body.userId) : undefined;
    const crewRole = trimmed(body.crewRole);
    const shiftStart = typeof body.shiftStart === 'string' ? body.shiftStart : '';
    const shiftEnd = typeof body.shiftEnd === 'string' ? body.shiftEnd : '';

    const errors: Record<string, string[]> = {};
    if (!person) errors.userId = ['That person does not exist or is not active.'];
    if (!crewRole) errors.crewRole = ["'Crew Role' must not be empty."];
    if (!shiftStart || !shiftEnd || shiftEnd <= shiftStart)
      errors.shiftEnd = ['The shift must end after it starts.'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    if (body.hourlyRate != null && !who.can('finance.view_staff_cost')) {
      return problem(403, 'Forbidden', 'Only the Director and Accounts can set an hourly rate.');
    }

    const shift: CrewAssignment = {
      assignmentId: crypto.randomUUID(),
      eventId: event.eventId,
      userId: person!.userId,
      fullName: person!.fullName,
      crewRole,
      shiftStart: new Date(shiftStart).toISOString(),
      shiftEnd: new Date(shiftEnd).toISOString(),
      confirmed: false,
      hourlyRate: typeof body.hourlyRate === 'number' ? body.hourlyRate : null,
    };
    mock.crew = [...mock.crew, shift];
    return HttpResponse.json(shiftFor(who, shift), { status: 201 });
  }),

  http.delete('/api/events/:eventId/crew/:assignmentId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('crew.assign')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    const shift = mock.crew.find(
      (candidate) => candidate.assignmentId === params.assignmentId && candidate.eventId === event?.eventId,
    );
    if (!event || !shift) return problem(404, 'Not found');
    mock.crew = mock.crew.filter((candidate) => candidate !== shift);
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/events/:eventId/allowed-transitions', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    return HttpResponse.json({
      eventId: event.eventId,
      current: event.status,
      allowed: allowedFrom[event.status],
    });
  }),

  http.post('/api/events/:eventId/transitions', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.transition')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');

    const { to, rowVersion } = await readJson(request);
    if (rowVersion !== event.rowVersion) return staleEvent(who, event);
    if (!allowedFrom[event.status].includes(to as EventStatus)) {
      return problem(409, 'Invalid transition', invalidMoveReason(event.status), {
        type: '/problems/invalid-transition',
      });
    }

    const moved = { ...event, status: to as EventStatus, rowVersion: nextRowVersion() };
    mock.events = mock.events.map((candidate) => (candidate.eventId === event.eventId ? moved : candidate));
    return HttpResponse.json(eventFor(who, moved));
  }),

  //a po or a deposit confirms an enquiry, which puts it on the board (FR-12)
  http.post('/api/events/:eventId/confirmation', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('confirmation.record')) return problem(403, 'Forbidden');

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    const body = await readJson(request);
    const errors: Record<string, string[]> = {};
    if (body.confirmationType === 'PurchaseOrder') {
      if (!trimmed(body.clientPoNumber)) errors.clientPoNumber = ["Enter the client's PO number."];
      if (!body.poReceivedDate) errors.poReceivedDate = ['Enter the date the PO was received.'];
    } else if (body.confirmationType === 'Deposit') {
      if (typeof body.depositAmount !== 'number' || body.depositAmount <= 0) {
        errors.depositAmount = ['Enter the deposit amount.'];
      }
      if (!body.depositPaidDate) errors.depositPaidDate = ['Enter the date the deposit was paid.'];
      if (!trimmed(body.depositReference)) errors.depositReference = ['Enter the payment reference.'];
    } else {
      errors.confirmationType = ['Choose a purchase order or a deposit.'];
    }
    if (Object.keys(errors).length > 0) return invalid(errors);
    if (event.status !== 'Enquired') {
      return problem(409, 'Invalid transition', 'This event is already confirmed.', {
        type: '/problems/invalid-transition',
      });
    }

    const confirmed = { ...event, status: 'ConfirmedInPlanning' as const, rowVersion: nextRowVersion() };
    mock.events = mock.events.map((candidate) =>
      candidate.eventId === event.eventId ? confirmed : candidate,
    );
    return HttpResponse.json(
      {
        confirmationId: crypto.randomUUID(),
        eventId: event.eventId,
        confirmationType: body.confirmationType,
        clientPoNumber: orNull(body.clientPoNumber),
        poReceivedDate: orNull(body.poReceivedDate),
        poAmount: typeof body.poAmount === 'number' ? body.poAmount : null,
        depositAmount: typeof body.depositAmount === 'number' ? body.depositAmount : null,
        depositPaidDate: orNull(body.depositPaidDate),
        depositReference: orNull(body.depositReference),
        documentId: null,
        confirmedAt: new Date().toISOString(),
      },
      { status: 201 },
    );
  }),

  //TODO(plan): the event form needs these two and the contract has neither. asked of C, mocked
  //here in the shape asked for
  http.get('/api/clients', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.create') && !who.can('event.edit')) return problem(403, 'Forbidden');

    const q = (new URL(request.url).searchParams.get('q') ?? '').trim().toLowerCase();
    const items = demoClients().filter((client) => client.name.toLowerCase().includes(q));
    return HttpResponse.json({ items, page: 1, pageSize: 50, total: items.length });
  }),

  http.get('/api/divisions', async ({ request }) => {
    await delay();
    return caller(request) ? HttpResponse.json(demoDivisions()) : unauthorised();
  }),
];

//the lifecycle from plan section 8.3. cancelled is only reachable from confirmed / in planning
const allowedFrom: Record<EventStatus, EventStatus[]> = {
  Enquired: [],
  ConfirmedInPlanning: ['InProgress', 'Cancelled'],
  InProgress: ['Finished'],
  Finished: [],
  Cancelled: [],
};

function invalidMoveReason(from: EventStatus) {
  if (from === 'Finished' || from === 'Cancelled')
    return 'Finished and cancelled events stay where they are.';
  if (from === 'InProgress')
    return "A live event can only move on to Finished, it can't go back or be cancelled.";
  return "That stage can't be reached from here.";
}

//staff rates go only to director and accounts, or to the person the shift belongs to
function shiftFor(who: Caller, shift: CrewAssignment): CrewAssignment {
  return who.can('finance.view_staff_cost') || shift.userId === who.me.userId
    ? shift
    : without(shift, 'hourlyRate');
}

//----------------------------------------------------------\\
//                              EVENT FORM CHECKS
//----------------------------------------------------------\\

type CheckedEvent = Omit<
  EventDetail,
  | 'eventId'
  | 'status'
  | 'createdByUserId'
  | 'serviceSchedule'
  | 'createdAt'
  | 'isActive'
  | 'rowVersion'
  | 'packSizeActual'
  | 'headcountConfirmed'
>;

//the api's validators and reference checks, with its wording
function checkEvent(
  who: Caller,
  body: Record<string, unknown>,
  exceptEventId: string | null,
): CheckedEvent | Response {
  const errors: Record<string, string[]> = {};
  const code = trimmed(body.eventCode);
  const name = trimmed(body.name);
  const client = demoClients().find((candidate) => candidate.clientId === body.clientId);
  const venue = mock.venues.find((candidate) => candidate.venueId === body.venueId && candidate.isActive);
  const division = demoDivisions().find((candidate) => candidate.divisionId === body.divisionId);
  const startsAt = typeof body.startsAt === 'string' ? body.startsAt : '';
  const endsAt = typeof body.endsAt === 'string' ? body.endsAt : '';
  const whole = (value: unknown) => (typeof value === 'number' && Number.isInteger(value) ? value : NaN);

  if (!/^[A-Za-z0-9-]{4,20}$/.test(code)) errors.eventCode = ['Use letters, numbers and hyphens only.'];
  else if (mock.events.some((event) => event.eventCode === code && event.eventId !== exceptEventId)) {
    errors.eventCode = ['That event code is already in use.'];
  }
  if (!name) errors.name = ["'Name' must not be empty."];
  if (!client) errors.clientId = ['That client does not exist or is not active.'];
  if (!body.venueId) errors.venueId = ['Choose a venue.'];
  else if (!venue) errors.venueId = ['That venue does not exist or is not active.'];
  if (!division) errors.divisionId = ['That division does not exist.'];
  if (!body.eventDate) errors.eventDate = ['Choose the event date.'];
  if (!startsAt) errors.startsAt = ['Choose when the event starts.'];
  if (!endsAt || (startsAt && endsAt <= startsAt)) errors.endsAt = ['The event must end after it starts.'];
  if (!(whole(body.packSizeEstimated) >= 0)) errors.packSizeEstimated = ['Enter the expected pack size.'];
  if (!(whole(body.staffRequired) >= 0)) errors.staffRequired = ['Enter how many staff are needed.'];
  if (Object.keys(errors).length > 0) return invalid(errors);

  const budget = typeof body.budgetAmount === 'number' ? body.budgetAmount : null;
  if (budget !== null && !who.can('finance.view_client_price')) {
    return problem(403, 'Forbidden', 'You cannot set the budget on an event.');
  }

  return {
    eventCode: code,
    name,
    clientId: client!.clientId,
    clientName: client!.name,
    venueId: venue!.venueId,
    venueName: venue!.name,
    divisionId: division!.divisionId,
    divisionCode: division!.code,
    eventType: body.eventType as EventDetail['eventType'],
    eventDate: String(body.eventDate),
    startsAt: new Date(startsAt).toISOString(),
    endsAt: new Date(endsAt).toISOString(),
    packSizeEstimated: whole(body.packSizeEstimated),
    headcountExpected: typeof body.headcountExpected === 'number' ? body.headcountExpected : null,
    paymentMode: body.paymentMode as EventDetail['paymentMode'],
    infrastructureMode: body.infrastructureMode as EventDetail['infrastructureMode'],
    staffRequired: whole(body.staffRequired),
    isConfidential: body.isConfidential === true,
    budgetAmount: budget,
  };
}

//----------------------------------------------------------\\
//                              CARDS
//----------------------------------------------------------\\

//the api's rules for both boards, with its wording, so the screens meet the same refusals they will
//for real. admin columns go by position: assigned, in progress / needs review, complete (FR-19, FR-20).
//event board cards are open, or done in the done column (D-009)
const adminStatusAt: CardStatus[] = ['Assigned', 'InProgressOrNeedsReview', 'Complete'];

function statusIn(board: Board, column: BoardColumn): CardStatus {
  if (board.boardType === 'Admin') return adminStatusAt[column.position] ?? 'Assigned';
  return column.isDoneColumn ? 'Done' : 'Open';
}

const isAssignee = (card: TaskCard, userId: string) =>
  card.assignees.some((person) => person.userId === userId);

type FoundCard = { board: Board; column: BoardColumn; card: TaskCard };

function findCard(cardId: unknown): FoundCard | null {
  for (const board of [mock.adminBoard, ...mock.eventBoards]) {
    for (const column of board.columns) {
      const card = column.cards.find((candidate) => candidate.cardId === cardId);
      if (card) return { board, column, card };
    }
  }
  return null;
}

//desk roles see every card, crew only their own (FR-21)
function canSeeCard(who: Caller, { board, card }: FoundCard) {
  const mine = isAssignee(card, who.me.userId);
  if (board.boardType === 'Admin') return who.can('admin_task.view') && (mine || seesAll(who));
  return canSeeEvent(who, board.eventId!) && (mine || seesAll(who));
}

function visibleCard(who: Caller, cardId: unknown) {
  const found = findCard(cardId);
  return found && canSeeCard(who, found) ? found : null;
}

const renumber = (cards: TaskCard[]) => cards.map((card, index) => ({ ...card, position: index }));

//takes the card out of its column and puts it in another (or the same) at a position, renumbering both
function place(
  board: Board,
  card: TaskCard,
  from: BoardColumn,
  to: BoardColumn,
  position: number,
  changes: Partial<TaskCard> = {},
) {
  from.cards = renumber(from.cards.filter((candidate) => candidate.cardId !== card.cardId));
  const moved: TaskCard = {
    ...card,
    ...changes,
    columnId: to.columnId,
    status: statusIn(board, to),
    rowVersion: nextRowVersion(),
  };
  const others = to.cards.filter((candidate) => candidate.cardId !== card.cardId);
  others.splice(Math.min(Math.max(position, 0), others.length), 0, moved);
  to.cards = renumber(others);
  return to.cards.find((candidate) => candidate.cardId === card.cardId)!;
}

//the card swapped for a changed copy where it sits, with a new version
function replace({ column, card }: FoundCard, changes: Partial<TaskCard>) {
  const updated: TaskCard = { ...card, ...changes, rowVersion: nextRowVersion() };
  column.cards = column.cards.map((candidate) => (candidate.cardId === card.cardId ? updated : candidate));
  return updated;
}

const staleCard = (card: TaskCard) =>
  problem(
    409,
    'Changed by someone else',
    'Someone changed this card while you had it open. Reload to see the latest.',
    { type: '/problems/concurrency-conflict', current: card },
  );

const invalidMove = (detail: string) =>
  problem(409, "That move isn't allowed", detail, { type: '/problems/invalid-transition' });

const missingVersion = () =>
  invalid({ rowVersion: ["Send the card's rowVersion, so a change made meanwhile isn't overwritten."] });

const offTheCrew = () =>
  problem(422, 'Business rule', "Add them to the event's crew first, then assign the card.");

export const cardHandlers = [
  http.get('/api/boards/admin', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('admin_task.view')) return problem(403, 'Forbidden');

    const columns = mock.adminBoard.columns.map((column) => ({
      ...column,
      cards: seesAll(who) ? column.cards : column.cards.filter((card) => isAssignee(card, who.me.userId)),
    }));
    return HttpResponse.json({ ...mock.adminBoard, columns });
  }),

  //crew get only the cards assigned to them, filtered before anything leaves the api (FR-21)
  http.get('/api/events/:eventId/board', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    const board = mock.eventBoards.find((candidate) => candidate.eventId === event.eventId);
    if (!board) return problem(404, 'Not found', 'This event has no task board.');

    const columns = board.columns.map((column) => ({
      ...column,
      cards: seesAll(who) ? column.cards : column.cards.filter((card) => isAssignee(card, who.me.userId)),
    }));
    return HttpResponse.json({ ...board, columns });
  }),

  http.get('/api/cards/:cardId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const found = visibleCard(who, params.cardId);
    return found ? HttpResponse.json(found.card) : problem(404, 'Not found');
  }),

  http.post('/api/boards/:boardId/cards', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const board = [mock.adminBoard, ...mock.eventBoards].find(
      (candidate) => candidate.boardId === params.boardId,
    );
    if (!board || (board.eventId && !canSeeEvent(who, board.eventId))) return problem(404, 'Not found');

    const body = await readJson(request);
    const subject = trimmed(body.subject);
    const assigneeIds = Array.isArray(body.assigneeIds) ? (body.assigneeIds as string[]) : [];
    if (!subject) return invalid({ subject: ['Give the card a subject.'] });

    let column: BoardColumn | undefined;
    if (board.boardType === 'Admin') {
      if (!who.can('admin_task.assign'))
        return problem(403, 'Forbidden', "Your role can't hand out admin tasks.");
      if (assigneeIds.length === 0) return invalid({ assigneeIds: ['Choose who should do this task.'] });
      column = board.columns[0];
    } else {
      //crew hold task.edit for their own cards only, adding one takes the full permission
      if (!who.can('task.edit') || !seesAll(who)) return problem(403, 'Forbidden');
      column = body.columnId
        ? board.columns.find((candidate) => candidate.columnId === body.columnId)
        : board.columns[0];
      if (!column) return problem(422, 'Business rule', "That column isn't on this board.");
      if (assigneeIds.some((userId) => !canOpenEvent(board.eventId!, userId))) return offTheCrew();
    }

    const assignees = assigneeIds.map(personRefFor);
    if (!column || assignees.some((person) => !person)) {
      return invalid({ assigneeIds: ["That person isn't an active user."] });
    }

    const card: TaskCard = {
      cardId: crypto.randomUUID(),
      boardId: board.boardId,
      columnId: column.columnId,
      subject,
      description: orNull(body.description),
      priority: (body.priority as TaskCard['priority']) ?? 'Normal',
      dueAt: typeof body.dueAt === 'string' ? new Date(body.dueAt).toISOString() : null,
      position: column.cards.length,
      status: statusIn(board, column),
      milestoneId: orNull(body.milestoneId),
      assignees: assignees as UserRef[],
      createdBy: who.me,
      reviewNotes: null,
      returnedBy: null,
      returnedAt: null,
      completedAt: column.isDoneColumn ? new Date().toISOString() : null,
      attachmentCount: 0,
      rowVersion: nextRowVersion(),
    };
    column.cards = [...column.cards, card];
    return HttpResponse.json(card, { status: 201 });
  }),

  //every editable field is replaced, so one left out is cleared
  http.patch('/api/cards/:cardId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const found = visibleCard(who, params.cardId);
    if (!found) return problem(404, 'Not found');
    const admin = found.board.boardType === 'Admin';
    if (!who.can(admin ? 'admin_task.edit' : 'task.edit')) return problem(403, 'Forbidden');

    const body = await readJson(request);
    const subject = trimmed(body.subject);
    if (!subject) return invalid({ subject: ['Give the card a subject.'] });
    if (!body.rowVersion) return missingVersion();
    if (admin && body.milestoneId) {
      return problem(422, 'Business rule', "Admin tasks aren't tied to an event milestone.");
    }
    if (body.rowVersion !== found.card.rowVersion) return staleCard(found.card);

    return HttpResponse.json(
      replace(found, {
        subject,
        description: orNull(body.description),
        priority: (body.priority as TaskCard['priority']) ?? 'Normal',
        dueAt: typeof body.dueAt === 'string' ? new Date(body.dueAt).toISOString() : null,
        milestoneId: orNull(body.milestoneId),
      }),
    );
  }),

  //the whole set of people, at the card's version
  http.put('/api/cards/:cardId/assignees', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const found = visibleCard(who, params.cardId);
    if (!found) return problem(404, 'Not found');

    const body = await readJson(request);
    const userIds = Array.isArray(body.userIds) ? (body.userIds as string[]) : null;
    if (!userIds) return invalid({ userIds: ["Send the full list of people, even if it's empty."] });
    if (!body.rowVersion) return missingVersion();

    if (found.board.boardType === 'Admin') {
      if (!who.can('admin_task.assign')) return problem(403, 'Forbidden');
      if (userIds.length === 0) return invalid({ assigneeIds: ['Choose who should do this task.'] });
    } else {
      if (!who.can('task.edit')) return problem(403, 'Forbidden');
      if (userIds.some((userId) => !canOpenEvent(found.board.eventId!, userId))) return offTheCrew();
    }

    const assignees = userIds.map(personRefFor);
    if (assignees.some((person) => !person)) {
      return invalid({ assigneeIds: ['Pick people who have an active Carbonate account.'] });
    }
    if (body.rowVersion !== found.card.rowVersion) return staleCard(found.card);

    return HttpResponse.json(replace(found, { assignees: assignees as UserRef[] }));
  }),

  //on the admin board a drag only reorders a column or hands a task in. event cards go anywhere
  //on their own board, and the done column marks them done
  http.post('/api/cards/:cardId/move', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const found = visibleCard(who, params.cardId);
    if (!found) return problem(404, 'Not found');
    const { board, card, column: from } = found;

    const { columnId, position, rowVersion } = await readJson(request);
    const to = board.columns.find((column) => column.columnId === columnId);
    if (!to) return problem(422, 'Business rule', "That column isn't on this card's board.");
    const at = typeof position === 'number' ? position : to.cards.length;

    if (board.boardType === 'Admin') {
      if (to !== from && !(from.position === 0 && to.position === 1)) {
        return invalidMove('Use Complete or Return to move a task on from review.');
      }
      if (to !== from && !isAssignee(card, who.me.userId)) {
        return problem(403, 'Forbidden', 'Only someone this task is assigned to can hand it in.');
      }
      if (rowVersion !== card.rowVersion) return staleCard(card);
      return HttpResponse.json(place(board, card, from, to, at));
    }

    if (!who.can('task.move')) return problem(403, 'Forbidden');
    if (rowVersion !== card.rowVersion) return staleCard(card);
    const completedAt = to.isDoneColumn ? (card.completedAt ?? new Date().toISOString()) : null;
    return HttpResponse.json(place(board, card, from, to, at, { completedAt }));
  }),

  http.post('/api/cards/:cardId/complete', async ({ request, params }) =>
    signOff(request, params.cardId, null),
  ),

  http.post('/api/cards/:cardId/return', async ({ request, params }) => {
    const body = await readJson(request);
    const notes = typeof body.reviewNotes === 'string' ? body.reviewNotes.trim() : '';
    if (!notes) return invalid({ reviewNotes: ['Say what still needs doing before it comes back.'] });
    return signOff(request, params.cardId, notes, body.rowVersion);
  }),
];

//complete when notes is null, return otherwise. the return body has already been read for its
//notes, so its row version is passed in
async function signOff(request: Request, cardId: unknown, notes: string | null, sentVersion?: unknown) {
  await delay();
  const who = caller(request);
  if (!who) return unauthorised();
  if (!who.can('admin_task.review')) return problem(403, 'Forbidden');

  const found = visibleCard(who, cardId);
  if (!found) return problem(404, 'Not found');
  const { board, card, column } = found;
  if (board.boardType !== 'Admin') {
    return invalidMove('Event cards are finished by moving them into the done column.');
  }
  const rowVersion = notes === null ? (await readJson(request)).rowVersion : sentVersion;

  if (card.createdBy.userId !== who.me.userId) {
    return problem(403, 'Forbidden', 'Only the manager who handed out this task can complete or return it.');
  }
  if (isAssignee(card, who.me.userId)) {
    return problem(403, 'Forbidden', 'A task assigned to you has to be signed off by someone else.');
  }
  if (column.position !== 1) {
    return invalidMove("Only a task that's been handed in for review can be completed or returned.");
  }
  if (rowVersion !== card.rowVersion) return staleCard(card);

  const [assigned, , complete] = board.columns;
  const updated =
    notes === null
      ? place(board, card, column, complete!, complete!.cards.length, {
          completedAt: new Date().toISOString(),
        })
      : place(board, card, column, assigned!, assigned!.cards.length, {
          reviewNotes: notes,
          returnedBy: who.me,
          returnedAt: new Date().toISOString(),
          completedAt: null,
        });
  return HttpResponse.json(updated);
}

export const handlers = [
  ...authHandlers,
  ...eventHandlers,
  ...venueHandlers,
  ...stockHandlers,
  ...financeHandlers,
  ...cardHandlers,
  ...settingsHandlers,
];
