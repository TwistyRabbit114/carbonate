import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { MeResponse } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { mfaStep, signedInSession } from '@/test/fixtures/sessions';
import { users } from '@/test/fixtures/users';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderApp';

const setupUri = 'otpauth://totp/Carbonate:bookkeeper@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Carbonate';

//logs in with a password and gets the mfa step back, as director and accounts do
async function passwordStep(me: MeResponse, enrol = false, route = '/login') {
  const view = renderApp({ me: null, route });
  server.use(
    http.get('/api/me', () => HttpResponse.json(me)),
    http.post('/api/auth/login', () => HttpResponse.json(mfaStep('mfa-1', enrol))),
  );

  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Email'), me.user.email);
  await user.type(screen.getByLabelText('Password'), 'correct horse battery');
  await user.click(screen.getByRole('button', { name: 'Log in' }));

  return { ...view, user };
}

//----------------------------------------------------------\\
//                              VERIFY
//----------------------------------------------------------\\

describe('mfa verify', () => {
  it('asks the director for a code, accepts it with a space, then carries on', async () => {
    let sent: unknown;
    let auth: string | null = 'not called';
    const { router, user } = await passwordStep(users.director, false, '/login?next=%2Fstock');
    server.use(
      http.post('/api/auth/mfa/verify', async ({ request }) => {
        sent = await request.json();
        auth = request.headers.get('Authorization');
        return HttpResponse.json(signedInSession('token'));
      }),
    );

    await screen.findByRole('heading', { name: 'Enter your code' });
    expect(router.state.location.pathname).toBe('/login/mfa');

    await user.type(screen.getByLabelText('6-digit code'), '123 456');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/stock'));
    //verify is the one mfa call that takes the token in the body rather than as the bearer
    expect(sent).toEqual({ mfaToken: 'mfa-1', code: '123456' });
    expect(auth).toBeNull();
  });

  it('says so when the code is wrong, and offers a way to start again', async () => {
    const { user } = await passwordStep(users.director);
    server.use(http.post('/api/auth/mfa/verify', () => new HttpResponse(null, { status: 401 })));

    await user.type(await screen.findByLabelText('6-digit code'), '000000');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("That code didn't work.");
    expect(screen.getByRole('link', { name: 'Start again' })).toHaveAttribute('href', '/login');
  });

  it('checks the code looks right before sending it', async () => {
    const verify = vi.fn(() => HttpResponse.json(signedInSession('token')));
    const { user } = await passwordStep(users.director);
    server.use(http.post('/api/auth/mfa/verify', verify));

    await user.type(await screen.findByLabelText('6-digit code'), '12ab');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByText('Enter the 6-digit code from your authenticator app')).toBeInTheDocument();
    expect(verify).not.toHaveBeenCalled();
  });

  it('sends anyone who lands here without a password step back to login', async () => {
    const { router } = renderApp({ me: null, route: '/login/mfa' });

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });

  it('has no accessibility violations', async () => {
    const { container } = await passwordStep(users.director);
    await screen.findByRole('heading', { name: 'Enter your code' });

    expect(await axeViolations(container)).toEqual([]);
  });
});

//----------------------------------------------------------\\
//                              FIRST-TIME SETUP
//----------------------------------------------------------\\

describe('mfa setup', () => {
  it('shows the setup key, confirms the first code and lands accounts on finance', async () => {
    const enrolCalls: Array<{ auth: string | null; body: string }> = [];
    const confirmCalls: Array<{ auth: string | null; body: unknown }> = [];
    server.use(
      http.post('/api/auth/mfa/enrol', async ({ request }) => {
        enrolCalls.push({ auth: request.headers.get('Authorization'), body: await request.text() });
        return HttpResponse.json({ otpauthUri: setupUri });
      }),
      http.post('/api/auth/mfa/confirm', async ({ request }) => {
        confirmCalls.push({ auth: request.headers.get('Authorization'), body: await request.json() });
        return HttpResponse.json(signedInSession('token'));
      }),
    );
    const { router, user } = await passwordStep(users.accounts, true);
    const meAuth: Array<string | null> = [];
    server.use(
      http.get('/api/me', ({ request }) => {
        meAuth.push(request.headers.get('Authorization'));
        return HttpResponse.json(users.accounts);
      }),
    );

    expect(await screen.findByText('JBSW Y3DP EHPK 3PXP')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /open it in your authenticator app/i })).toHaveAttribute(
      'href',
      setupUri,
    );

    //switching to the authenticator app and back must not mint a new secret
    fireEvent(window, new Event('visibilitychange'));
    fireEvent(window, new Event('focus'));

    await user.type(screen.getByLabelText('6-digit code'), '654321');
    await user.click(screen.getByRole('button', { name: 'Finish setup' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/finance'));

    //enrol and confirm carry the mfa token as the bearer, and only once there's a session
    //does the real access token take over
    expect(enrolCalls).toEqual([{ auth: 'Bearer mfa-1', body: '' }]);
    expect(confirmCalls).toEqual([{ auth: 'Bearer mfa-1', body: { code: '654321' } }]);
    expect(meAuth).toEqual(['Bearer token']);
  });

  it('offers a way to start again if the sign-in ran out while setting up the app', async () => {
    server.use(
      http.post('/api/auth/mfa/enrol', () => HttpResponse.json({ otpauthUri: setupUri })),
      http.post('/api/auth/mfa/confirm', () => new HttpResponse(null, { status: 401 })),
    );
    const { user } = await passwordStep(users.accounts, true);

    await user.type(await screen.findByLabelText('6-digit code'), '654321');
    await user.click(screen.getByRole('button', { name: 'Finish setup' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('or start again');
    expect(screen.getByRole('link', { name: 'Start again' })).toHaveAttribute('href', '/login');
  });

  it("refuses a setup link that isn't an otpauth uri", async () => {
    server.use(
      http.post('/api/auth/mfa/enrol', () => HttpResponse.json({ otpauthUri: 'javascript:alert(1)' })),
    );
    await passwordStep(users.accounts, true);

    expect(await screen.findByRole('alert')).toHaveTextContent("We couldn't start the setup.");
    expect(screen.queryByRole('link', { name: /authenticator app/i })).not.toBeInTheDocument();
  });
});
