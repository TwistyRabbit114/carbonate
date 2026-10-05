import { delay, http, HttpResponse } from 'msw';
import type { BoardColumn, CardStatus, EventStatus, TaskCard, UserRef } from '@/api/types';
import { permissionCheck, type Permission } from '@/auth/permissions';
import { demoAdminBoard } from '../fixtures/adminTasks';
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

//back to signed out with the demo data as it started, tests call this before each run
export function resetMocks() {
  mockSession = null;
  mockEvents = demoEvents();
  mockAdminBoard = demoAdminBoard();
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
//                              EVENTS
//----------------------------------------------------------\\

export const eventHandlers = [
  //desk roles see every event. crew only see events they're assigned to, and crew
  //assignments aren't mocked yet, so their list comes back empty for now
  http.get('/api/events', async ({ request }) => {
    await delay();
    const key = accountFromBearer(request.headers.get('Authorization'));
    if (!key) return new HttpResponse(null, { status: 401 });

    const visible = permissionCheck(users[key].permissions).can('event.view_all') ? mockEvents : [];
    const boardOnly = new URL(request.url).searchParams.get('board') === 'true';
    const items = boardOnly
      ? visible.filter((event) => event.status !== 'Enquired' && event.status !== 'Cancelled')
      : visible;

    return HttpResponse.json({ items, page: 1, pageSize: 200, total: items.length });
  }),

  http.get('/api/events/:eventId/allowed-transitions', async ({ request, params }) => {
    await delay();
    if (!accountFromBearer(request.headers.get('Authorization')))
      return new HttpResponse(null, { status: 401 });

    const event = mockEvents.find((candidate) => candidate.eventId === params.eventId);
    if (!event) return problem(404, 'Not found');
    return HttpResponse.json({ eventId: event.eventId, current: event.status, allowed: allowedFrom[event.status] });
  }),

  http.post('/api/events/:eventId/transitions', async ({ request, params }) => {
    await delay();
    const key = accountFromBearer(request.headers.get('Authorization'));
    if (!key) return new HttpResponse(null, { status: 401 });
    if (!permissionCheck(users[key].permissions).can('event.transition')) return problem(403, 'Forbidden');

    const index = mockEvents.findIndex((candidate) => candidate.eventId === params.eventId);
    const event = mockEvents[index];
    if (!event) return problem(404, 'Not found');

    const { to, rowVersion } = await readJson(request);
    if (rowVersion !== event.rowVersion) {
      return HttpResponse.json(
        {
          type: '/problems/concurrency-conflict',
          title: 'Changed by someone else',
          status: 409,
          current: event,
        },
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    if (!allowedFrom[event.status].includes(to as EventStatus)) {
      return HttpResponse.json(
        {
          type: '/problems/invalid-transition',
          title: 'Invalid transition',
          status: 409,
          detail: invalidMoveReason(event.status),
        },
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
      );
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
//                              ADMIN TASKS
//----------------------------------------------------------\\

//the api's rules from AdminTaskRules, with its wording, so the board meets the same refusals it
//will for real. columns go by position: assigned, in progress / needs review, complete (FR-19, FR-20)
const adminStatusAt: CardStatus[] = ['Assigned', 'InProgressOrNeedsReview', 'Complete'];

function caller(request: Request) {
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

const isAssignee = (card: TaskCard, userId: string) => card.assignees.some((person) => person.userId === userId);

//desk roles see every admin task, crew only their own, and anything else is a 404 (FR-21)
const seesAll = (who: NonNullable<ReturnType<typeof caller>>) => who.can('event.view_all');

function findAdminCard(cardId: unknown) {
  for (const column of mockAdminBoard.columns) {
    const card = column.cards.find((candidate) => candidate.cardId === cardId);
    if (card) return { card, column };
  }
  return null;
}

const renumber = (cards: TaskCard[]) => cards.map((card, index) => ({ ...card, position: index }));

//takes the card out of its column and puts it in another (or the same) at a position, renumbering both
function place(card: TaskCard, from: BoardColumn, to: BoardColumn, position: number, changes: Partial<TaskCard> = {}) {
  from.cards = renumber(from.cards.filter((candidate) => candidate.cardId !== card.cardId));
  const moved: TaskCard = {
    ...card,
    ...changes,
    columnId: to.columnId,
    status: adminStatusAt[to.position] ?? card.status,
    rowVersion: nextRowVersion(),
  };
  const others = to.cards.filter((candidate) => candidate.cardId !== card.cardId);
  others.splice(Math.min(Math.max(position, 0), others.length), 0, moved);
  to.cards = renumber(others);
  return to.cards.find((candidate) => candidate.cardId === card.cardId)!;
}

const staleCard = (card: TaskCard) =>
  problem(409, 'Changed by someone else', undefined, { type: '/problems/concurrency-conflict', current: card });

const invalidMove = (detail: string) =>
  problem(409, "That move isn't allowed", detail, { type: '/problems/invalid-transition' });

export const adminTaskHandlers = [
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

  http.post('/api/boards/:boardId/cards', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();
    if (params.boardId !== mockAdminBoard.boardId) return problem(404, 'Not found');

    const body = await readJson(request);
    const subject = typeof body.subject === 'string' ? body.subject.trim() : '';
    const assigneeIds = Array.isArray(body.assigneeIds) ? (body.assigneeIds as string[]) : [];
    if (!subject) return invalid({ subject: ['Give the card a subject.'] });
    if (!who.can('admin_task.assign')) return problem(403, 'Forbidden', "Your role can't hand out admin tasks.");
    if (assigneeIds.length === 0) return invalid({ assigneeIds: ['An admin task needs someone to do it.'] });

    const assignees = assigneeIds.map(personRefFor);
    if (assignees.some((person) => !person)) return invalid({ assigneeIds: ["That person isn't an active user."] });

    const column = mockAdminBoard.columns[0]!;
    const card: TaskCard = {
      cardId: crypto.randomUUID(),
      boardId: mockAdminBoard.boardId,
      columnId: column.columnId,
      subject,
      description: typeof body.description === 'string' ? body.description : null,
      priority: (body.priority as TaskCard['priority']) ?? 'Normal',
      dueAt: typeof body.dueAt === 'string' ? new Date(body.dueAt).toISOString() : null,
      position: column.cards.length,
      status: 'Assigned',
      milestoneId: null,
      assignees: assignees as UserRef[],
      createdBy: who.me,
      reviewNotes: null,
      returnedBy: null,
      returnedAt: null,
      completedAt: null,
      attachmentCount: 0,
      rowVersion: nextRowVersion(),
    };
    column.cards = [...column.cards, card];
    return HttpResponse.json(card, { status: 201 });
  }),

  //a drag on the admin board only reorders a column or hands a task in
  http.post('/api/cards/:cardId/move', async ({ request, params }) => {
    await delay();
    const who = caller(request);
    if (!who) return unauthorised();

    const found = findAdminCard(params.cardId);
    if (!found || (!seesAll(who) && !isAssignee(found.card, who.me.userId))) return problem(404, 'Not found');
    const { card, column: from } = found;

    const { columnId, position, rowVersion } = await readJson(request);
    const to = mockAdminBoard.columns.find((column) => column.columnId === columnId);
    if (!to) return invalid({ columnId: ["That column isn't on this board."] });

    if (to !== from && !(from.position === 0 && to.position === 1)) {
      return invalidMove('Use Complete or Return to move a task on from review.');
    }
    if (to !== from && !isAssignee(card, who.me.userId)) {
      return problem(403, 'Forbidden', 'Only someone this task is assigned to can hand it in.');
    }
    if (rowVersion !== card.rowVersion) return staleCard(card);

    return HttpResponse.json(place(card, from, to, typeof position === 'number' ? position : to.cards.length));
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

  const found = findAdminCard(cardId);
  if (!found) return problem(404, 'Not found');
  const { card, column } = found;
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

  const [assigned, , complete] = mockAdminBoard.columns;
  const updated =
    notes === null
      ? place(card, column, complete!, complete!.cards.length, { completedAt: new Date().toISOString() })
      : place(card, column, assigned!, assigned!.cards.length, {
          reviewNotes: notes,
          returnedBy: who.me,
          returnedAt: new Date().toISOString(),
          completedAt: null,
        });
  return HttpResponse.json(updated);
}

export const handlers = [...authHandlers, ...eventHandlers, ...adminTaskHandlers];
