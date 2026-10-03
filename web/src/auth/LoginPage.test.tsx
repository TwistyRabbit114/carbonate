import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { axeViolations } from '@/test/axe';
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
        return HttpResponse.json({ accessToken: 'token', user: users.eventManager.user });
      }),
    );

    await fillAndSubmit('sarah@example.com', 'correct horse battery');

    expect(await screen.findByRole('heading', { level: 1, name: 'Events Board' })).toBeInTheDocument();
    expect(sent).toEqual({ email: 'sarah@example.com', password: 'correct horse battery' });
  });

  it('goes back to the page the user was heading for', async () => {
    const { router } = openLogin('/login?next=%2Fstock');
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json({ accessToken: 'token', user: users.eventManager.user }),
      ),
    );

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

  it("doesn't say whether the email or the password was wrong", async () => {
    openLogin();
    server.use(
      http.post('/api/auth/login', () => HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })),
    );

    await fillAndSubmit('sarah@example.com', 'wrong password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
  });

  it('explains a lockout', async () => {
    openLogin();
    server.use(http.post('/api/auth/login', () => new HttpResponse(null, { status: 423 })));

    await fillAndSubmit('sarah@example.com', 'wrong again');

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts. Try again in 15 minutes.');
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
