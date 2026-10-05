import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { axeViolations } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';

const panel = (name: string) => screen.findByRole('region', { name });
const dialog = (name: string) => screen.findByRole('dialog', { name });

//----------------------------------------------------------\\
//                              WHO SEES WHAT
//----------------------------------------------------------\\

describe('SettingsPage panels', () => {
  it('gives the director all four, with nothing for axe to flag', async () => {
    const { container } = renderMockedApp('director', '/settings');

    expect(await screen.findByRole('heading', { level: 1, name: 'Settings' })).toBeInTheDocument();
    await within(await panel('Users')).findByText('Sarah M.');
    await within(await panel('Google Calendar')).findByText(/Connected to events-calendar@example\.com/);
    await within(await panel('Venues')).findByText('Steenberg Estate');
    await within(await panel('Audit log')).findByText('Event updated');

    expect(await axeViolations(container)).toEqual([]);
  });

  it('gives operations everything but the audit log', async () => {
    renderMockedApp('operationsManager', '/settings');

    await panel('Users');
    await panel('Google Calendar');
    await panel('Venues');
    expect(screen.queryByRole('region', { name: 'Audit log' })).not.toBeInTheDocument();
  });

  it('gives an event manager just the venues', async () => {
    renderMockedApp('eventManager', '/settings');

    await within(await panel('Venues')).findByText('Steenberg Estate');
    expect(screen.queryByRole('region', { name: 'Users' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Google Calendar' })).not.toBeInTheDocument();
  });

  it('turns crew away at the route', async () => {
    renderMockedApp('crewLead', '/settings');

    expect(await screen.findByText(/isn't available for your role/i)).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              USERS
//----------------------------------------------------------\\

describe('SettingsPage users', () => {
  it('adds a user who then shows in the list', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/settings');
    await within(await panel('Users')).findByText('Sarah M.');

    await user.click(within(await panel('Users')).getByRole('button', { name: 'Add user' }));
    const form = await dialog('Add user');
    await user.type(within(form).getByLabelText(/^Name/), 'Zola K.');
    await user.type(within(form).getByLabelText(/^Email/), 'zola@example.com');
    await user.type(within(form).getByLabelText(/^Employee number/), 'CB-007');
    await user.selectOptions(within(form).getByLabelText(/^Employment/), 'Casual');
    await user.click(within(form).getByRole('checkbox', { name: 'Casual Crew' }));
    await user.type(within(form).getByLabelText(/^First password/), 'a-long-enough-password');
    await user.click(within(form).getByRole('button', { name: 'Add user' }));

    expect(await screen.findByText('Zola K. can sign in now.')).toBeInTheDocument();
    const row = (await within(await panel('Users')).findByText('Zola K.')).closest('tr')!;
    expect(within(row).getByText('Casual Crew')).toBeInTheDocument();
    expect(within(row).getByText('Casual: ends 7 days after their last debrief')).toBeInTheDocument();
  });

  it('checks the form before sending, and puts the api refusal on the right field', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/settings');
    await within(await panel('Users')).findByText('Sarah M.');

    await user.click(within(await panel('Users')).getByRole('button', { name: 'Add user' }));
    const form = await dialog('Add user');
    await user.click(within(form).getByRole('button', { name: 'Add user' }));
    expect(await within(form).findByText('Choose at least one role.')).toBeInTheDocument();
    expect(within(form).getByText('Use at least 12 characters.')).toBeInTheDocument();

    //sarah's address is taken
    await user.type(within(form).getByLabelText(/^Name/), 'Sarah Again');
    await user.type(within(form).getByLabelText(/^Email/), 'sarah@example.com');
    await user.type(within(form).getByLabelText(/^Employee number/), 'CB-099');
    await user.click(within(form).getByRole('checkbox', { name: 'Event Manager' }));
    await user.type(within(form).getByLabelText(/^First password/), 'a-long-enough-password');
    await user.click(within(form).getByRole('button', { name: 'Add user' }));

    expect(await within(form).findByText('Someone already has that email address.')).toBeInTheDocument();
    expect(within(form).getByLabelText(/^Email/)).toHaveAttribute('aria-invalid', 'true');
  });

  it("says plainly when the api won't let the director switch themselves off", async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/settings');
    const users = await panel('Users');
    await within(users).findByText('Sarah M.');

    await user.click(within(users).getByRole('button', { name: 'Edit Director' }));
    const form = await dialog('Edit Director');
    await user.click(within(form).getByRole('checkbox', { name: 'Can sign in' }));
    await user.click(within(form).getByRole('button', { name: 'Save changes' }));

    expect(await within(form).findByText('You cannot deactivate your own account.')).toBeInTheDocument();
  });

  it('switches someone off, and only sends what changed', async () => {
    const user = userEvent.setup();
    let sent: unknown;
    renderMockedApp('operationsManager', '/settings');
    server.use(
      http.patch('/api/users/:userId', async ({ request }) => {
        sent = await request.clone().json();
        return undefined;
      }),
    );
    const users = await panel('Users');
    await within(users).findByText('Priya R.');

    await user.click(within(users).getByRole('button', { name: 'Edit Priya R.' }));
    const form = await dialog('Edit Priya R.');
    await user.click(within(form).getByRole('checkbox', { name: 'Can sign in' }));
    await user.click(within(form).getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText("Priya R.'s account is updated.")).toBeInTheDocument();
    expect(sent).toEqual({ isActive: false });
    const row = (await within(await panel('Users')).findByText('Priya R.')).closest('tr')!;
    expect(within(row).getByText('Switched off')).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              AUDIT
//----------------------------------------------------------\\

describe('SettingsPage audit log', () => {
  it('shows what changed in words, and filters by record', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/settings');
    const audit = await panel('Audit log');
    const row = (await within(audit).findByText('Event updated')).closest('tr')!;

    await user.click(within(row).getByText('2 changes'));
    expect(within(row).getByText('Pack size estimated')).toBeInTheDocument();
    expect(within(row).getByText(/160 to 180/)).toBeInTheDocument();

    //the last page stays up while the filtered one loads
    await user.selectOptions(within(audit).getByLabelText('Record'), 'Venues');
    await waitFor(() => expect(within(audit).queryByText('Event updated')).not.toBeInTheDocument());
    expect(within(audit).getByText('Venue created')).toBeInTheDocument();
  });

  it('says so when nothing changed in the range', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/settings');
    const audit = await panel('Audit log');
    await within(audit).findByText('Event updated');

    await user.type(within(audit).getByLabelText('From'), '2020-01-01');
    await user.type(within(audit).getByLabelText('To'), '2020-01-31');

    expect(await within(audit).findByText('Nothing was changed in that range.')).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              GOOGLE CALENDAR
//----------------------------------------------------------\\

describe('SettingsPage google calendar', () => {
  it('disconnects after asking, then offers to connect again', async () => {
    const user = userEvent.setup();
    renderMockedApp('director', '/settings');
    const calendar = await panel('Google Calendar');
    await within(calendar).findByText(/Connected to/);
    expect(
      within(calendar).getByText('Outbound only. Carbonate never reads from Google.'),
    ).toBeInTheDocument();

    await user.click(within(calendar).getByRole('button', { name: 'Disconnect' }));
    await user.click(
      within(await dialog('Disconnect Google Calendar?')).getByRole('button', { name: 'Disconnect' }),
    );

    expect(await within(calendar).findByText('Not connected')).toBeInTheDocument();
    await user.click(within(calendar).getByRole('button', { name: 'Connect Google Calendar' }));
    expect(await within(calendar).findByText(/Connected to/)).toBeInTheDocument();
  });

  it('warns when google needs reconnecting', async () => {
    renderMockedApp('director', '/settings');
    server.use(
      http.get('/api/calendar/connection', () =>
        HttpResponse.json({
          connected: true,
          googleAccountEmail: 'events-calendar@example.com',
          connectedAt: '2026-09-01T08:00:00Z',
          reconnectNeeded: true,
          lastPushedAt: null,
          lastError: 'invalid_grant',
        }),
      ),
    );
    const calendar = await panel('Google Calendar');

    expect(await within(calendar).findByText(/Reconnect needed/)).toBeInTheDocument();
    expect(within(calendar).getByRole('button', { name: 'Reconnect' })).toBeInTheDocument();
  });

  it("says the sync isn't switched on while the api still answers 501", async () => {
    renderMockedApp('director', '/settings');
    server.use(
      http.get('/api/calendar/connection', () =>
        HttpResponse.json({ status: 501, title: 'Not built yet' }, { status: 501 }),
      ),
    );
    const calendar = await panel('Google Calendar');

    expect(await within(calendar).findByText(/isn't switched on yet/)).toBeInTheDocument();
    expect(within(calendar).queryByRole('button')).not.toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              VENUES
//----------------------------------------------------------\\

describe('SettingsPage venues', () => {
  it('adds a venue, checking the opening hours come as a pair', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', '/settings');
    const venues = await panel('Venues');
    await within(venues).findByText('Steenberg Estate');

    await user.click(within(venues).getByRole('button', { name: 'Add venue' }));
    const form = await dialog('Add venue');
    await user.type(within(form).getByLabelText(/^Name/), 'Old Mill');
    await user.type(within(form).getByLabelText(/^Address/), '1 Mill Lane, Newlands');
    await user.type(within(form).getByLabelText('Opens'), '08:00');
    await user.click(within(form).getByRole('button', { name: 'Add venue' }));
    expect(
      await within(form).findByText('Add the closing time too, or leave both times empty.'),
    ).toBeInTheDocument();

    await user.type(within(form).getByLabelText(/^Closes/), '01:00');
    await user.click(within(form).getByRole('checkbox', { name: 'Security clearance' }));
    await user.click(within(form).getByRole('button', { name: 'Add venue' }));

    expect(await screen.findByText('Old Mill is added.')).toBeInTheDocument();
    const row = (await within(await panel('Venues')).findByText('Old Mill')).closest('tr')!;
    expect(within(row).getByText('08:00 to 01:00')).toBeInTheDocument();
    expect(within(row).getByText('Security clearance')).toBeInTheDocument();
  });

  it('takes a venue out of use without deleting it', async () => {
    const user = userEvent.setup();
    renderMockedApp('operationsManager', '/settings');
    const venues = await panel('Venues');
    await within(venues).findByText('The Point Hotel');

    await user.click(within(venues).getByRole('button', { name: 'Edit The Point Hotel' }));
    const form = await dialog('Edit The Point Hotel');
    await user.click(within(form).getByRole('checkbox', { name: 'Offer this venue for new events' }));
    await user.click(within(form).getByRole('button', { name: 'Save changes' }));

    const row = (await within(await panel('Venues')).findByText('The Point Hotel')).closest('tr')!;
    expect(await within(row).findByText('Not in use')).toBeInTheDocument();
  });
});
