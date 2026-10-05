import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { axeViolations } from '@/test/axe';
import { mfaStep, signedInSession } from '@/test/fixtures/sessions';
import { users } from '@/test/fixtures/users';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderApp';

//starts signed out on the login page, with /me answering for whoever logs in
function openLogin(route = '/login', signsInAs = users.eventManager) {
  const view = renderApp({ me: null, route });
  server.use(http.get('/api/me', () => HttpResponse.json(signsInAs)));
  return view;
}

async function fillAndSubmit(email: string, password: string) {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText('Email'), email);
  await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: 'Log in' }));
}

//----------------------------------------------------------\\
//                              HAPPY PATH
//----------------------------------------------------------\\

describe('login', () => {
  it("logs in and lands on the role's start page", async () => {
    let sent: unknown;
    openLogin();
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(signedInSession('token'));
      }),
    );

    await fillAndSubmit('sarah@example.com', 'correct horse battery');

    expect(await screen.findByRole('heading', { level: 1, name: 'Events Board' })).toBeInTheDocument();
    expect(sent).toEqual({ email: 'sarah@example.com', password: 'correct horse battery' });
  });

  //every login answer carries mfaRequired, so it's the value that decides, not the key being there
  it('goes to the code step only when mfaRequired is true', async () => {
    const { router } = openLogin('/login', users.director);
    server.use(http.post('/api/auth/login', () => HttpResponse.json(mfaStep('mfa-1'))));

    await fillAndSubmit('director@example.com', 'correct horse battery');

    await waitFor(() => expect(router.state.location.pathname).toBe('/login/mfa'));
  });

  it('goes back to the page the user was heading for', async () => {
    const { router } = openLogin('/login?next=%2Fstock');
    server.use(http.post('/api/auth/login', () => HttpResponse.json(signedInSession('token'))));

    await fillAndSubmit('sarah@example.com', 'correct horse battery');

    await waitFor(() => expect(router.state.location.pathname).toBe('/stock'));
  });

  it('sends someone who is already signed in straight on', async () => {
    const { router } = renderApp({ me: users.director, route: '/login' });

    await waitFor(() => expect(router.state.location.pathname).toBe('/events'));
  });
});

//----------------------------------------------------------\\
//                              ERRORS
//----------------------------------------------------------\\

describe('login errors', () => {
  it('checks the fields before sending anything', async () => {
    const login = vi.fn(() => HttpResponse.json({}));
    openLogin();
    server.use(http.post('/api/auth/login', login));

    await userEvent.click(await screen.findByRole('button', { name: 'Log in' }));

    expect(await screen.findByText('Enter your email address')).toBeInTheDocument();
    expect(screen.getByText('Enter your password')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
    expect(login).not.toHaveBeenCalled();
  });

  //the api answers a wrong password and a locked account with the same 401 on purpose
  it("doesn't say whether the email or password was wrong, or whether the account is locked", async () => {
    openLogin();
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json(
          {
            type: '/problems/unauthenticated',
            title: 'Sign in required.',
            status: 401,
            detail: 'That email and password do not match, or the account is temporarily locked.',
          },
          { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    await fillAndSubmit('sarah@example.com', 'wrong password');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Email or password is incorrect. After too many tries the account locks for 15 minutes.',
    );
  });

  it('asks the user to slow down when the login rate limit kicks in', async () => {
    openLogin();
    server.use(http.post('/api/auth/login', () => new HttpResponse(null, { status: 429 })));

    await fillAndSubmit('sarah@example.com', 'wrong again');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many attempts. Wait a minute and try again.',
    );
  });

  it('says when the api cannot be reached', async () => {
    openLogin();
    server.use(http.post('/api/auth/login', () => HttpResponse.error()));

    await fillAndSubmit('sarah@example.com', 'correct horse battery');

    expect(await screen.findByRole('alert')).toHaveTextContent("Can't reach Carbonate right now.");
  });

  it('explains why the user is back on login after their session ran out', async () => {
    openLogin('/login?next=%2Fevents&reason=expired');

    expect(await screen.findByText('Your session has ended. Log in again to carry on.')).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              ACCESSIBILITY
//----------------------------------------------------------\\

describe('login accessibility', () => {
  it('has no violations, including with errors showing', async () => {
    const { container } = openLogin();
    await userEvent.click(await screen.findByRole('button', { name: 'Log in' }));
    await screen.findByText('Enter your email address');

    expect(await axeViolations(container)).toEqual([]);
  });
});
