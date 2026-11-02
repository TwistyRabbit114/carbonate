import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { signedInSession } from '@/test/fixtures/sessions';
import { accessTokenFor, breachedPassword, demoPassword } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';

const panel = (name: string) => screen.findByRole('region', { name });
const newPassword = 'a-long-enough-password';

async function fillIn(current: string, next: string, repeat = next) {
  const user = userEvent.setup();
  const form = await panel('Change password');
  if (current) await user.type(within(form).getByLabelText(/^Current password/), current);
  if (next) await user.type(within(form).getByLabelText(/^New password/), next);
  if (repeat) await user.type(within(form).getByLabelText(/^Confirm new password/), repeat);
  await user.click(within(form).getByRole('button', { name: 'Change password' }));
  return form;
}

//----------------------------------------------------------\\
//                              GETTING THERE
//----------------------------------------------------------\\

describe('AccountPage', () => {
  it('opens from your name, says who you are, with nothing for axe to flag', async () => {
    const { container, router } = renderMockedApp('eventManager', '/events');

    await userEvent.click(await screen.findByRole('link', { name: /^Your account, Sarah M\./ }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Your account' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/account');
    const details = await panel('Details');
    expect(within(details).getByText('sarah@example.com')).toBeInTheDocument();
    expect(within(details).getByText('Event Manager')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /^Your account/ })).toHaveAttribute('aria-current', 'page');

    expect(await axeViolations(container)).toEqual([]);
  });

  it('is there for casual crew too, who have no settings', async () => {
    renderMockedApp('casualCrew', '/account');

    const details = await panel('Details');
    expect(within(details).getByText('Priya R.')).toBeInTheDocument();
    expect(within(details).getByText('Casual Crew')).toBeInTheDocument();
    expect(await panel('Change password')).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              CHANGING THE PASSWORD
//----------------------------------------------------------\\

describe('AccountPage change password', () => {
  it('sends the current and new passwords, then clears the form', async () => {
    let sent: unknown;
    renderMockedApp('crewLead', '/account');
    server.use(
      http.post('/api/auth/password', async ({ request }) => {
        sent = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const form = await fillIn(demoPassword, newPassword);

    expect(
      await screen.findByText('Password changed. Use the new one next time you log in.'),
    ).toBeInTheDocument();
    expect(sent).toEqual({ currentPassword: demoPassword, newPassword });
    for (const box of within(form).getAllByLabelText(/password/)) expect(box).toHaveValue('');
  });

  it('checks the form before sending anything', async () => {
    let calls = 0;
    renderMockedApp('crewLead', '/account');
    server.use(
      http.post('/api/auth/password', () => {
        calls += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    const form = await fillIn('', '');
    expect(await within(form).findByText('Enter your current password.')).toBeInTheDocument();
    expect(within(form).getByText('Use at least 12 characters.')).toBeInTheDocument();
    expect(within(form).getByText('Type the new password again.')).toBeInTheDocument();

    await userEvent.clear(within(form).getByLabelText(/^Current password/));
    await fillIn(demoPassword, newPassword, `${newPassword}!`);
    expect(await within(form).findByText("That doesn't match the new password.")).toBeInTheDocument();
    expect(within(form).getByLabelText(/^Confirm new password/)).toHaveAttribute('aria-invalid', 'true');
    expect(calls).toBe(0);
  });

  it('puts a wrong current password on its own box, and moves focus there', async () => {
    renderMockedApp('crewLead', '/account');

    const form = await fillIn('not-my-password', newPassword);

    expect(await within(form).findByText('That is not your current password.')).toBeInTheDocument();
    const box = within(form).getByLabelText(/^Current password/);
    expect(box).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(box).toHaveFocus());
  });

  it('turns away a password from a known breach', async () => {
    renderMockedApp('crewLead', '/account');

    const form = await fillIn(demoPassword, breachedPassword);

    expect(
      await within(form).findByText('That password has appeared in a data breach. Choose a different one.'),
    ).toBeInTheDocument();
    expect(within(form).getByLabelText(/^New password/)).toHaveAttribute('aria-invalid', 'true');
  });

  it('says plainly when the server has no such endpoint yet', async () => {
    renderMockedApp('crewLead', '/account');
    server.use(http.post('/api/auth/password', () => new HttpResponse(null, { status: 404 })));

    const form = await fillIn(demoPassword, newPassword);

    expect(
      await within(form).findByText("Changing your password isn't switched on yet."),
    ).toBeInTheDocument();
  });

  //the access token lasts 15 minutes, so someone who signed in a while ago still gets through
  it('refreshes a lapsed sign-in and tries again', async () => {
    let calls = 0;
    renderMockedApp('crewLead', '/account');
    server.use(
      http.post('/api/auth/password', () => {
        calls += 1;
        return new HttpResponse(null, { status: calls === 1 ? 401 : 204 });
      }),
      http.post('/api/auth/refresh', () => HttpResponse.json(signedInSession(accessTokenFor('crewLead')))),
    );

    await fillIn(demoPassword, newPassword);

    expect(
      await screen.findByText('Password changed. Use the new one next time you log in.'),
    ).toBeInTheDocument();
    await waitFor(() => expect(calls).toBe(2));
  });
});
