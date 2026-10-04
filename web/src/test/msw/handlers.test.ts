import { beforeEach, describe, expect, it } from 'vitest';
import { users } from '../fixtures/users';
import { demoMfaCode, demoPassword, handlers, resetMockSession } from './handlers';
import { server } from './server';

//the mock session lives in module memory, so each test starts signed out
beforeEach(() => {
  resetMockSession();
  server.use(...handlers);
});

function post(path: string, body: unknown) {
  return fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

async function login(key: keyof typeof users, password = demoPassword) {
  return post('/api/auth/login', { email: users[key].user.email, password });
}

//----------------------------------------------------------\\
//                              LOGIN
//----------------------------------------------------------\\

describe('mock login', () => {
  it.each(['operationsManager', 'eventManager', 'crewLead', 'casualCrew'] as const)(
    'signs %s straight in, and /me answers with that account',
    async (key) => {
      const { accessToken } = (await (await login(key)).json()) as { accessToken: string };
      const me = await fetch('/api/me', { headers: { Authorization: `Bearer ${accessToken}` } });

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
    const step = (await (await login('director')).json()) as {
      mfaToken: string;
      mfaEnrolmentRequired: boolean;
    };
    expect(step.mfaEnrolmentRequired).toBe(false);

    expect((await post('/api/auth/mfa/verify', { mfaToken: step.mfaToken, code: '000000' })).status).toBe(
      401,
    );
    const ok = await post('/api/auth/mfa/verify', { mfaToken: step.mfaToken, code: demoMfaCode });
    expect(await ok.json()).toMatchObject({ accessToken: 'mock-access-director' });
  });

  it('sends the bookkeeper through first-time setup', async () => {
    const step = (await (await login('accounts')).json()) as {
      mfaToken: string;
      mfaEnrolmentRequired: boolean;
    };
    expect(step.mfaEnrolmentRequired).toBe(true);

    const enrol = (await (await post('/api/auth/mfa/enrol', { mfaToken: step.mfaToken })).json()) as {
      otpauthUri: string;
    };
    expect(enrol.otpauthUri).toMatch(/^otpauth:\/\/totp\/.+secret=/);

    const done = await post('/api/auth/mfa/confirm', { mfaToken: step.mfaToken, code: demoMfaCode });
    expect(await done.json()).toMatchObject({ accessToken: 'mock-access-accounts' });
  });
});

//----------------------------------------------------------\\
//                              SESSION
//----------------------------------------------------------\\

describe('mock session', () => {
  it('refreshes the signed-in account until it logs out', async () => {
    await login('crewLead');

    const refreshed = await post('/api/auth/refresh', {});
    expect(await refreshed.json()).toEqual({ accessToken: 'mock-access-crewLead' });

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
