import { delay, http, HttpResponse } from 'msw';
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
//                              SESSION
//----------------------------------------------------------\\

//the real api keeps the session in an HttpOnly refresh cookie. msw would save a mocked cookie
//to localStorage, so the mock holds it in memory instead and a full reload signs you out
let mockSession: AccountKey | null = null;

export function resetMockSession() {
  mockSession = null;
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

export const handlers = [...authHandlers];
