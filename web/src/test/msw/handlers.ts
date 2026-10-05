import { delay, http, HttpResponse } from 'msw';
import type { Board, BoardColumn, CardStatus, EventStatus, TaskCard, UserRef } from '@/api/types';
import { permissionCheck, type Permission } from '@/auth/permissions';
import { demoAdminBoard } from '../fixtures/adminTasks';
import { demoEventBoard } from '../fixtures/eventBoards';
import { demoCrew, demoMilestones, eventDetailFor } from '../fixtures/eventDetails';
import { demoEvents } from '../fixtures/events';
import { mfaStep, signedInSession } from '../fixtures/sessions';
import { userList, users } from '../fixtures/users';

//a stand-in for the api so screens can be built and demoed before the real endpoints exist.
//loaded by `npm run dev:mocks` only, and never part of the production build

//----------------------------------------------------------\\
//                              DEMO ACCOUNTS
//----------------------------------------------------------\\

type AccountKey = keyof typeof users;

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

//----------------------------------------------------------\\
//                              TOKENS
//----------------------------------------------------------\\

//mock tokens just name the account, there is nothing secret in them
const accessTokenFor = (key: AccountKey) => `mock-access-${key}`;
const mfaTokenFor = (key: AccountKey) => `mock-mfa-${key}`;

function isAccount(value: string | undefined): value is AccountKey {
  return value !== undefined && value in users;
}

function accountFromBearer(header: string | null) {
  const key = header?.replace(/^Bearer mock-access-/, '');
  return isAccount(key) ? key : null;
}

function accountFromMfaToken(token: unknown) {
  const key = typeof token === 'string' ? token.replace(/^mock-mfa-/, '') : undefined;
  return isAccount(key) ? key : null;
}

//mfa enrol and confirm get the mfa token as the bearer, like the real api
function accountFromMfaBearer(header: string | null) {
  return accountFromMfaToken(header?.replace(/^Bearer /, ''));
}

//----------------------------------------------------------\\
//                              STATE
//----------------------------------------------------------\\

//the real api keeps the session in an HttpOnly refresh cookie. msw would save a mocked cookie
//to localStorage, so the mock holds it in memory instead and a full reload signs you out
let mockSession: AccountKey | null = null;
let mockEvents = demoEvents();
let mockAdminBoard = demoAdminBoard();
let mockEventBoards = mockEvents.map(demoEventBoard);
let mockCrew = mockEvents.flatMap(demoCrew);

//back to signed out with the demo data as it started, tests call this before each run
export function resetMocks() {
  mockSession = null;
  mockEvents = demoEvents();
  mockAdminBoard = demoAdminBoard();
  mockEventBoards = mockEvents.map(demoEventBoard);
  mockCrew = mockEvents.flatMap(demoCrew);
}

//----------------------------------------------------------\\
//                              RESPONSES
//----------------------------------------------------------\\

function problem(status: number, title: string, detail?: string, extra: object = {}) {
  return HttpResponse.json(
    { status, title, detail, ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

const invalid = (errors: Record<string, string[]>) =>
  problem(400, 'One or more validation errors occurred.', undefined, { errors });

const unauthorised = () => new HttpResponse(null, { status: 401 });

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
  mockSession = key;
  return HttpResponse.json(signedInSession(accessTokenFor(key)));
}

async function readJson(request: Request) {
  try {
    return (await request.json()) as Record<string, unknown>;
  } catch {
    return {};
  }
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
    if (!mockSession) return new HttpResponse(null, { status: 401 });
    return HttpResponse.json(signedInSession(accessTokenFor(mockSession)));
  }),

  http.post('/api/auth/logout', () => {
    mockSession = null;
    return new HttpResponse(null, { status: 204 });
  }),

  http.get('/api/me', ({ request }) => {
    const key = accountFromBearer(request.headers.get('Authorization'));
    return key ? HttpResponse.json(users[key]) : new HttpResponse(null, { status: 401 });
  }),
];

//----------------------------------------------------------\\
//                              WHO IS ASKING
//----------------------------------------------------------\\

type Caller = { key: AccountKey; me: UserRef; can: (code: Permission) => boolean };

function caller(request: Request): Caller | null {
  const key = accountFromBearer(request.headers.get('Authorization'));
  if (!key) return null;
  const check = permissionCheck(users[key].permissions);
  return {
    key,
    me: personRefFor(users[key].user.userId)!,
    can: (code: Permission) => check.can(code),
  };
}

function personRefFor(userId: string): UserRef | undefined {
  const account = Object.values(users).find((candidate) => candidate.user.userId === userId);
  return account && { userId, fullName: account.user.fullName };
}

//desk roles see every event, crew only the ones they're crewed on (plan section 7.2). anything
//else is a 404, never a 403, so a hidden event doesn't give itself away
const seesAll = (who: Caller) => who.can('event.view_all');
const onCrew = (eventId: string, userId: string) =>
  mockCrew.some((shift) => shift.eventId === eventId && shift.userId === userId);
const canSeeEvent = (who: Caller, eventId: string) => seesAll(who) || onCrew(eventId, who.me.userId);

//someone who can open the event, so a card can go to them
function canOpenEvent(eventId: string, userId: string) {
  const account = Object.values(users).find((candidate) => candidate.user.userId === userId);
  return Boolean(account && (permissionCheck(account.permissions).can('event.view_all') || onCrew(eventId, userId)));
}

function visibleEvent(who: Caller, eventId: unknown) {
  const event = mockEvents.find((candidate) => candidate.eventId === eventId);
  return event && canSeeEvent(who, event.eventId) ? event : undefined;
}

//----------------------------------------------------------\\
//                              EVENTS
//----------------------------------------------------------\\

export const eventHandlers = [
  http.get('/api/events', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const visible = mockEvents.filter((event) => canSeeEvent(who, event.eventId));
    const boardOnly = new URL(request.url).searchParams.get('board') === 'true';
    const items = boardOnly
      ? visible.filter((event) => event.status !== 'Enquired' && event.status !== 'Cancelled')
      : visible;

    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  //no budget in the mock's event, it's a $price field and nothing built yet shows it
  http.get('/api/events/:eventId', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    return event ? HttpResponse.json(eventDetailFor(event)) : problem(404, 'Not found');
  }),

  http.get('/api/events/:eventId/milestones', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    return event ? HttpResponse.json(demoMilestones(event)) : problem(404, 'Not found');
  }),

  http.get('/api/events/:eventId/crew', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    return HttpResponse.json(mockCrew.filter((shift) => shift.eventId === event.eventId));
  }),

  http.get('/api/events/:eventId/allowed-transitions', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    return HttpResponse.json({ eventId: event.eventId, current: event.status, allowed: allowedFrom[event.status] });
  }),

  http.post('/api/events/:eventId/transitions', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('event.transition')) return problem(403, 'Forbidden');

    const index = mockEvents.findIndex((candidate) => candidate.eventId === params.eventId);
    const event = mockEvents[index];
    if (!event) return problem(404, 'Not found');

    const { to, rowVersion } = await readJson(request);
    if (rowVersion !== event.rowVersion) {
      return problem(409, 'Changed by someone else', undefined, {
        type: '/problems/concurrency-conflict',
        current: event,
      });
    }
    if (!allowedFrom[event.status].includes(to as EventStatus)) {
      return problem(409, 'Invalid transition', invalidMoveReason(event.status), {
        type: '/problems/invalid-transition',
      });
    }

    const moved = { ...event, status: to as EventStatus, rowVersion: nextRowVersion() };
    mockEvents[index] = moved;
    return HttpResponse.json(moved);
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

let rowVersionCounter = 0;
function nextRowVersion() {
  rowVersionCounter++;
  return btoa(`mock-version-${rowVersionCounter}`);
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

const isAssignee = (card: TaskCard, userId: string) => card.assignees.some((person) => person.userId === userId);

type FoundCard = { board: Board; column: BoardColumn; card: TaskCard };

function findCard(cardId: unknown): FoundCard | null {
  for (const board of [mockAdminBoard, ...mockEventBoards]) {
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

const trimmed = (value: unknown) => (typeof value === 'string' ? value.trim() : '');
const orNull = (value: unknown) => (typeof value === 'string' && value ? value : null);

export const cardHandlers = [
  http.get('/api/boards/admin', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('admin_task.view')) return problem(403, 'Forbidden');

    const columns = mockAdminBoard.columns.map((column) => ({
      ...column,
      cards: seesAll(who) ? column.cards : column.cards.filter((card) => isAssignee(card, who.me.userId)),
    }));
    return HttpResponse.json({ ...mockAdminBoard, columns });
  }),

  //crew get only the cards assigned to them, filtered before anything leaves the api (FR-21)
  http.get('/api/events/:eventId/board', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const event = visibleEvent(who, params.eventId);
    if (!event) return problem(404, 'Not found');
    const board = mockEventBoards.find((candidate) => candidate.eventId === event.eventId);
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

    const board = [mockAdminBoard, ...mockEventBoards].find((candidate) => candidate.boardId === params.boardId);
    if (!board || (board.eventId && !canSeeEvent(who, board.eventId))) return problem(404, 'Not found');

    const body = await readJson(request);
    const subject = trimmed(body.subject);
    const assigneeIds = Array.isArray(body.assigneeIds) ? (body.assigneeIds as string[]) : [];
    if (!subject) return invalid({ subject: ['Give the card a subject.'] });

    let column: BoardColumn | undefined;
    if (board.boardType === 'Admin') {
      if (!who.can('admin_task.assign')) return problem(403, 'Forbidden', "Your role can't hand out admin tasks.");
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

  http.post('/api/cards/:cardId/complete', async ({ request, params }) => signOff(request, params.cardId, null)),

  http.post('/api/cards/:cardId/return', async ({ request, params }) => {
    const body = await readJson(request);
    const notes = typeof body.reviewNotes === 'string' ? body.reviewNotes.trim() : '';
    if (!notes) return invalid({ reviewNotes: ['Say what still needs doing before it comes back.'] });
    return signOff(request, params.cardId, notes, body.rowVersion);
  }),

  //only director and ops hold user.manage, the same people who hand tasks out
  http.get('/api/users', async ({ request }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (!who.can('user.manage')) return problem(403, 'Forbidden');

    const items = userList();
    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
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
      ? place(board, card, column, complete!, complete!.cards.length, { completedAt: new Date().toISOString() })
      : place(board, card, column, assigned!, assigned!.cards.length, {
          reviewNotes: notes,
          returnedBy: who.me,
          returnedAt: new Date().toISOString(),
          completedAt: null,
        });
  return HttpResponse.json(updated);
}

export const handlers = [...authHandlers, ...eventHandlers, ...cardHandlers];
