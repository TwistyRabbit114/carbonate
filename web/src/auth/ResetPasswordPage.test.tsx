import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderApp';

const token = 'a-single-use-token';
const newPassword = 'a-long-enough-password';
const expired = 'This reset link is not valid or has expired. Ask an administrator for a new one.';

//signed out, opening the link from the users panel
function openReset(route = `/login/reset#token=${token}`) {
  return renderApp({ me: null, route });
}

async function choose(password: string, repeat = password) {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText(/^New password/), password);
  await user.type(screen.getByLabelText(/^Confirm new password/), repeat);
  await user.click(screen.getByRole('button', { name: 'Save new password' }));
}

describe('ResetPasswordPage', () => {
  it('sends the token from the link with the new password, then sends them to log in', async () => {
    let sent: unknown;
    server.use(
      http.post('/api/auth/password/reset', async ({ request }) => {
        sent = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { container, router } = openReset();
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Choose a new password' }),
    ).toBeInTheDocument();
    expect(await axeViolations(container)).toEqual([]);

    await choose(newPassword);

    expect(await screen.findByRole('heading', { level: 1, name: 'Password changed' })).toBeInTheDocument();
    expect(sent).toEqual({ token, newPassword });
    await userEvent.click(screen.getByRole('link', { name: 'Log in' }));
    expect(router.state.location.pathname).toBe('/login');
  });

  it('checks the two boxes match before sending anything', async () => {
    let calls = 0;
    server.use(
      http.post('/api/auth/password/reset', () => {
        calls += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    openReset();

    await choose(newPassword, 'something-else-entirely');

    expect(await screen.findByText("That doesn't match the new password.")).toBeInTheDocument();
    expect(calls).toBe(0);
  });

  it('says plainly when the link is used up or out of date', async () => {
    server.use(
      http.post('/api/auth/password/reset', () =>
        HttpResponse.json(
          { status: 400, title: 'One or more validation errors occurred.', errors: { token: [expired] } },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    openReset();

    await choose(newPassword);

    expect(await screen.findByText(expired)).toBeInTheDocument();
  });

  it('puts a refused password on the new password box', async () => {
    server.use(
      http.post('/api/auth/password/reset', () =>
        HttpResponse.json(
          {
            status: 400,
            title: 'One or more validation errors occurred.',
            errors: { newPassword: ['That password has appeared in a data breach. Choose a different one.'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    openReset();

    await choose(newPassword);

    expect(
      await screen.findByText('That password has appeared in a data breach. Choose a different one.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/^New password/)).toHaveAttribute('aria-invalid', 'true');
  });

  it('asks for the whole link when the token is missing', async () => {
    openReset('/login/reset');

    expect(await screen.findByText(/This page needs the reset link/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save new password' })).not.toBeInTheDocument();
  });

  it('the login page says who to ask for a link', async () => {
    openReset('/login');

    expect(await screen.findByText(/give you a link to set a new one/)).toBeInTheDocument();
  });
});
