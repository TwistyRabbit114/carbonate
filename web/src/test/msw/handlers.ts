import { delay, http, HttpResponse } from 'msw';
import type { EventStatus } from '@/api/types';
import { permissionCheck } from '@/auth/permissions';
import { demoEvents } from '../fixtures/events';
import { users } from '../fixtures/users';

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
const mfaStep: Partial<Record<AccountKey, 'verify' | 'enrol'>> = {
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

//----------------------------------------------------------\\
//                              STATE
//----------------------------------------------------------\\

//the real api keeps the session in an HttpOnly refresh cookie. msw would save a mocked cookie
//to localStorage, so the mock holds it in memory instead and a full reload signs you out
let mockSession: AccountKey | null = null;
let mockEvents = demoEvents();

//back to signed out with the demo data as it started, tests call this before each run
export function resetMocks() {
  mockSession = null;
  mockEvents = demoEvents();
}

//----------------------------------------------------------\\
//                              RESPONSES
//----------------------------------------------------------\\

function problem(status: number, title: string) {
  return HttpResponse.json(
    { status, title },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

function signedIn(key: AccountKey) {
  mockSession = key;
  return HttpResponse.json({ accessToken: accessTokenFor(key), user: users[key].user });
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
    if (!key || password !== demoPassword) return problem(401, 'Email or password is incorrect');

    const step = mfaStep[key];
    if (!step) return signedIn(key);
    return HttpResponse.json({
      mfaRequired: true,
      mfaToken: mfaTokenFor(key),
      mfaEnrolmentRequired: step === 'enrol',
    });
  }),

  http.post('/api/auth/mfa/enrol', async ({ request }) => {
    await delay();
    const key = accountFromMfaToken((await readJson(request)).mfaToken);
    if (!key) return problem(401, 'Start again');

    const label = encodeURIComponent(`Carbonate:${users[key].user.email}`);
    return HttpResponse.json({
      otpauthUri: `otpauth://totp/${label}?secret=JBSWY3DPEHPK3PXP&issuer=Carbonate`,
    });
  }),

  ...['/api/auth/mfa/verify', '/api/auth/mfa/confirm'].map((path) =>
    http.post(path, async ({ request }) => {
      await delay();
      const body = await readJson(request);
      const key = accountFromMfaToken(body.mfaToken);
      if (!key || body.code !== demoMfaCode) return problem(401, 'Invalid code');
      return signedIn(key);
    }),
  ),

  http.post('/api/auth/refresh', () => {
    if (!mockSession) return new HttpResponse(null, { status: 401 });
    return HttpResponse.json({ accessToken: accessTokenFor(mockSession) });
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
    const boardOnly = new URL(request.url).searchParams.get('view') === 'board';
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
    return event ? HttpResponse.json(allowedFrom[event.status]) : problem(404, 'Not found');
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

export const handlers = [...authHandlers, ...eventHandlers];
