import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { MeResponse } from '@/api/types';
import { axeViolations } from '@/test/axe';
import { users } from '@/test/fixtures/users';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderApp';

const setupUri = 'otpauth://totp/Carbonate:bookkeeper@example.com?secret=JBSWY3DPEHPK3PXP&issuer=Carbonate';

//logs in with a password and gets the mfa step back, as director and accounts do
async function passwordStep(me: MeResponse, enrol = false, route = '/login') {
  const view = renderApp({ me: null, route });
  server.use(
    http.get('/api/me', () => HttpResponse.json(me)),
    http.post('/api/auth/login', () =>
      HttpResponse.json({ mfaRequired: true, mfaToken: 'mfa-1', mfaEnrolmentRequired: enrol }),
    ),
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
    const { router, user } = await passwordStep(users.director, false, '/login?next=%2Fstock');
    server.use(
      http.post('/api/auth/mfa/verify', async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json({ accessToken: 'token' });
      }),
    );

    await screen.findByRole('heading', { name: 'Enter your code' });
    expect(router.state.location.pathname).toBe('/login/mfa');

    await user.type(screen.getByLabelText('6-digit code'), '123 456');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/stock'));
    expect(sent).toEqual({ mfaToken: 'mfa-1', code: '123456' });
  });

  it('says so when the code is wrong', async () => {
    const { user } = await passwordStep(users.director);
    server.use(http.post('/api/auth/mfa/verify', () => new HttpResponse(null, { status: 401 })));

    await user.type(await screen.findByLabelText('6-digit code'), '000000');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("That code didn't work.");
  });

  it('checks the code looks right before sending it', async () => {
    const verify = vi.fn(() => HttpResponse.json({ accessToken: 'token' }));
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
    let enrolCalls = 0;
    const confirm = vi.fn(() => HttpResponse.json({ accessToken: 'token' }));
    server.use(
      http.post('/api/auth/mfa/enrol', () => {
        enrolCalls++;
        return HttpResponse.json({ otpauthUri: setupUri });
      }),
      http.post('/api/auth/mfa/confirm', confirm),
    );
    const { router, user } = await passwordStep(users.accounts, true);

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
    expect(confirm).toHaveBeenCalledOnce();
    expect(enrolCalls).toBe(1);
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
