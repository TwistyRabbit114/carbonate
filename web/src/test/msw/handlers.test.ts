import { beforeEach, describe, expect, it } from 'vitest';
import type { Board, MfaEnrolResponse, MfaStep, SessionResponse } from '@/api/types';
import { demoEvents } from '../fixtures/events';
import { mfaStep, signedInSession } from '../fixtures/sessions';
import { users } from '../fixtures/users';
import { demoMfaCode, demoPassword, handlers, resetMocks } from './handlers';
import { server } from './server';

//the mock keeps its session and data in module memory, so each test starts fresh
beforeEach(() => {
  resetMocks();
  server.use(...handlers);
});

function post(path: string, body: unknown, bearer?: string) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (bearer) headers.Authorization = `Bearer ${bearer}`;
  return fetch(path, { method: 'POST', headers, body: JSON.stringify(body) });
}

async function login(key: keyof typeof users, password = demoPassword) {
  return post('/api/auth/login', { email: users[key].user.email, password });
}

async function mfaLogin(key: keyof typeof users) {
  return (await (await login(key)).json()) as MfaStep;
}

//----------------------------------------------------------\\
//                              LOGIN
//----------------------------------------------------------\\

describe('mock login', () => {
  it.each(['operationsManager', 'eventManager', 'crewLead', 'casualCrew'] as const)(
    'signs %s straight in, and /me answers with that account',
    async (key) => {
      const session = (await (await login(key)).json()) as SessionResponse;
      expect(session).toEqual(signedInSession(`mock-access-${key}`));

      const me = await fetch('/api/me', { headers: { Authorization: `Bearer ${session.accessToken}` } });
      expect(await me.json()).toEqual(users[key]);
    },
  );

  it('turns away a wrong password', async () => {
    expect((await login('eventManager', 'nope')).status).toBe(401);
  });

  it('turns away an email that is not a demo account', async () => {
    const res = await post('/api/auth/login', { email: 'someone@example.com', password: demoPassword });
    expect(res.status).toBe(401);
  });
});

//----------------------------------------------------------\\
//                              MFA
//----------------------------------------------------------\\

describe('mock mfa', () => {
  it('asks the director for a code and only accepts the demo one', async () => {
    const step = await mfaLogin('director');
    expect(step).toEqual(mfaStep('mock-mfa-director'));

    expect((await post('/api/auth/mfa/verify', { mfaToken: step.mfaToken, code: '000000' })).status).toBe(
      401,
    );
    const ok = await post('/api/auth/mfa/verify', { mfaToken: step.mfaToken, code: demoMfaCode });
    expect(await ok.json()).toEqual(signedInSession('mock-access-director'));
  });

  it('sends the bookkeeper through first-time setup, with the mfa token as the bearer', async () => {
    const step = await mfaLogin('accounts');
    expect(step.mfaEnrolmentRequired).toBe(true);

    const enrol = await post('/api/auth/mfa/enrol', {}, step.mfaToken);
    expect(((await enrol.json()) as MfaEnrolResponse).otpauthUri).toMatch(/^otpauth:\/\/totp\/.+secret=/);

    const done = await post('/api/auth/mfa/confirm', { code: demoMfaCode }, step.mfaToken);
    expect(await done.json()).toEqual(signedInSession('mock-access-accounts'));
  });

  it('turns setup away when the mfa token comes in the body instead of the bearer', async () => {
    const step = await mfaLogin('accounts');

    expect((await post('/api/auth/mfa/enrol', { mfaToken: step.mfaToken })).status).toBe(401);
    expect((await post('/api/auth/mfa/confirm', { mfaToken: step.mfaToken, code: demoMfaCode })).status).toBe(
      401,
    );
  });
});

//----------------------------------------------------------\\
//                              SESSION
//----------------------------------------------------------\\

describe('mock session', () => {
  it('refreshes the signed-in account until it logs out', async () => {
    await login('crewLead');

    const refreshed = await post('/api/auth/refresh', {});
    expect(await refreshed.json()).toEqual(signedInSession('mock-access-crewLead'));

    await post('/api/auth/logout', {});
    expect((await post('/api/auth/refresh', {})).status).toBe(401);
  });

  it('refuses a refresh with no session', async () => {
    expect((await post('/api/auth/refresh', {})).status).toBe(401);
  });

  it('never writes anything to browser storage', async () => {
    localStorage.clear();
    await login('eventManager');

    expect(localStorage.length).toBe(0);
    expect(document.cookie).toBe('');
  });

  it('refuses /me without a token', async () => {
    expect((await fetch('/api/me')).status).toBe(401);
  });
});

//----------------------------------------------------------\
//                              BOARDS
//----------------------------------------------------------\

//signs in and calls the mock as that account
async function as(key: keyof typeof users) {
  const session = (await (await login(key)).json()) as SessionResponse;
  const headers = { Authorization: `Bearer ${session.accessToken}`, 'Content-Type': 'application/json' };
  return (path: string, init: RequestInit = {}) => fetch(path, { ...init, headers });
}

const vantage = demoEvents()[1]!.eventId;
const atlas = demoEvents()[5]!.eventId; //the enquiry, with nobody crewed on it

describe('mock boards', () => {
  it('gives crew only the cards assigned to them on an event board (FR-21)', async () => {
    const call = await as('casualCrew');
    const board = (await (await call(`/api/events/${vantage}/board`)).json()) as Board;
    const cards = board.columns.flatMap((column) => column.cards);

    expect(cards.length).toBeGreaterThan(0);
    expect(
      cards.every((card) => card.assignees.some((person) => person.userId === users.casualCrew.user.userId)),
    ).toBe(true);
  });

  it("answers 404 for an event the crew member isn't on, and leaves it out of their events", async () => {
    const call = await as('casualCrew');

    expect((await call(`/api/events/${atlas}/board`)).status).toBe(404);
    const events = (await (await call('/api/events')).json()) as { items: { eventId: string }[] };
    expect(events.items.map((event) => event.eventId)).not.toContain(atlas);
  });

  it('turns a reassign at a stale version away with the card as it is now', async () => {
    const call = await as('eventManager');
    const board = (await (await call(`/api/events/${vantage}/board`)).json()) as Board;
    const card = board.columns[0]!.cards[0]!;
    const url = `/api/cards/${card.cardId}/assignees`;

    const first = await call(url, {
      method: 'PUT',
      body: JSON.stringify({ userIds: [], rowVersion: card.rowVersion }),
    });
    const stale = await call(url, {
      method: 'PUT',
      body: JSON.stringify({ userIds: [], rowVersion: card.rowVersion }),
    });

    expect(first.status).toBe(200);
    expect(stale.status).toBe(409);
    expect(((await stale.json()) as { current: { cardId: string } }).current.cardId).toBe(card.cardId);
  });
});
