import { delay, http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { server } from '@/test/msw/server';
import { apiFetch, refreshSession, setAccessToken, setSessionExpiredHandler } from './client';
import { ApiError } from './problem';

const unauthorised = () => new HttpResponse(null, { status: 401 });

//the client keeps module state, so every test starts logged out with no listener
beforeEach(() => {
  setAccessToken(null);
  setSessionExpiredHandler(null);
});

//----------------------------------------------------------\\
//                              REQUESTS
//----------------------------------------------------------\\

describe('apiFetch', () => {
  it('sends the access token as a bearer header', async () => {
    setAccessToken('token-1');
    server.use(
      http.get('/api/me', ({ request }) => HttpResponse.json({ auth: request.headers.get('Authorization') })),
    );

    await expect(apiFetch('/me')).resolves.toEqual({ auth: 'Bearer token-1' });
  });

  it('serialises json bodies and sets the content type', async () => {
    server.use(
      http.post('/api/auth/login', async ({ request }) =>
        HttpResponse.json({ type: request.headers.get('Content-Type'), body: await request.json() }),
      ),
    );

    await expect(
      apiFetch('/auth/login', { method: 'POST', json: { email: 'sarah@example.com' } }),
    ).resolves.toEqual({
      type: 'application/json',
      body: { email: 'sarah@example.com' },
    });
  });

  it('returns nothing for a 204', async () => {
    server.use(http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })));

    await expect(apiFetch('/auth/logout', { method: 'POST' })).resolves.toBeUndefined();
  });

  it('turns problem details into an ApiError, keeping top-level extensions', async () => {
    server.use(
      http.put('/api/events/1', () =>
        HttpResponse.json(
          {
            type: '/problems/concurrency-conflict',
            title: 'Changed by someone else',
            current: { rowVersion: 'AAA=' },
          },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await apiFetch('/events/1', { method: 'PUT', json: {} }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).problem.type).toBe('/problems/concurrency-conflict');
    expect((error as ApiError).problem.current).toEqual({ rowVersion: 'AAA=' });
  });

  it('reports a network failure as status 0', async () => {
    server.use(http.get('/api/me', () => HttpResponse.error()));

    await expect(apiFetch('/me')).rejects.toHaveProperty('status', 0);
  });
});

//----------------------------------------------------------\\
//                              EXPIRED TOKENS
//----------------------------------------------------------\\

describe('when the access token has expired', () => {
  it('refreshes once and retries with the new token', async () => {
    setAccessToken('stale');
    server.use(
      http.post('/api/auth/refresh', () => HttpResponse.json({ accessToken: 'fresh' })),
      http.get('/api/me', ({ request }) =>
        request.headers.get('Authorization') === 'Bearer fresh'
          ? HttpResponse.json({ ok: true })
          : unauthorised(),
      ),
    );

    await expect(apiFetch('/me')).resolves.toEqual({ ok: true });
  });

  it('shares a single refresh between parallel requests', async () => {
    let refreshCalls = 0;
    setAccessToken('stale');
    server.use(
      http.post('/api/auth/refresh', async () => {
        refreshCalls++;
        await delay(20);
        return HttpResponse.json({ accessToken: 'fresh' });
      }),
      http.get('/api/events/:id', ({ request, params }) =>
        request.headers.get('Authorization') === 'Bearer fresh'
          ? HttpResponse.json({ id: params.id })
          : unauthorised(),
      ),
    );

    const results = await Promise.all(['1', '2', '3'].map((id) => apiFetch(`/events/${id}`)));

    expect(results).toEqual([{ id: '1' }, { id: '2' }, { id: '3' }]);
    expect(refreshCalls).toBe(1);
  });

  it('ends the session and drops the token when the refresh fails', async () => {
    const expired = vi.fn();
    const seenAuth: Array<string | null> = [];
    setSessionExpiredHandler(expired);
    setAccessToken('stale');
    server.use(
      http.post('/api/auth/refresh', unauthorised),
      http.get('/api/me', ({ request }) => {
        seenAuth.push(request.headers.get('Authorization'));
        return unauthorised();
      }),
    );

    await expect(apiFetch('/me')).rejects.toHaveProperty('status', 401);
    expect(expired).toHaveBeenCalledOnce();

    await apiFetch('/me').catch(() => undefined);
    expect(seenAuth).toEqual(['Bearer stale', null]);
  });

  it("doesn't refresh when login itself answers 401", async () => {
    const refresh = vi.fn(() => HttpResponse.json({ accessToken: 'x' }));
    server.use(
      http.post('/api/auth/refresh', refresh),
      http.post('/api/auth/login', () =>
        HttpResponse.json({ title: 'Email or password is incorrect' }, { status: 401 }),
      ),
    );

    await expect(apiFetch('/auth/login', { method: 'POST', json: {} })).rejects.toHaveProperty('status', 401);
    expect(refresh).not.toHaveBeenCalled();
  });
});

describe('refreshSession', () => {
  it('reports false when the api is unreachable', async () => {
    server.use(http.post('/api/auth/refresh', () => HttpResponse.error()));

    await expect(refreshSession()).resolves.toBe(false);
  });
});
