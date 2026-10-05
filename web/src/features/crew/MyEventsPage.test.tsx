import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { formatTime } from '@/lib/format';
import { axeViolations } from '@/test/axe';
import { demoCrew } from '@/test/fixtures/eventDetails';
import { demoEvents } from '@/test/fixtures/events';
import { users } from '@/test/fixtures/users';
import { renderApp } from '@/test/renderApp';
import { renderMockedApp } from '@/test/renderMockedApp';

const riverlight = demoEvents().find((event) => event.eventCode === 'RIV-FEST-26')!;
const naidoo = demoEvents().find((event) => event.eventCode === 'NAI-WED-26')!;
const callTimeAt = (event: typeof naidoo) =>
  formatTime(demoCrew(event).find((shift) => shift.userId === users.crewLead.user.userId)!.shiftStart);

describe('MyEventsPage', () => {
  it('puts the live event first with the call time big', async () => {
    renderMockedApp('crewLead', '/my/events');

    const next = await screen.findByRole('article', { name: 'Riverlight Festival' });
    expect(within(next).getByText('On now')).toBeInTheDocument();
    expect(within(next).getByText('Your call time')).toBeInTheDocument();
    expect(await within(next).findByText(callTimeAt(riverlight))).toBeInTheDocument();
    expect(await within(next).findByRole('link', { name: /Liesbeek Parkway/ })).toHaveAttribute(
      'href',
      'https://maps.google.com/?q=Liesbeek%20Parkway%2C%20Observatory%2C%20Cape%20Town',
    );
    expect(within(next).getByText('Hi-vis at all times during build.')).toBeInTheDocument();
  });

  it('lists what else is coming up, and keeps past events out of the way', async () => {
    renderMockedApp('crewLead', '/my/events');

    const coming = await screen.findByRole('region', { name: 'Coming up' });
    const names = within(coming)
      .getAllByRole('link')
      .map((link) => link.textContent);
    expect(names[0]).toMatch(/^Vantage Brand Activation/);
    expect(names.some((name) => name?.startsWith('Naidoo Wedding'))).toBe(true);
    //the enquiry has no crew yet, so it isn't theirs
    expect(screen.queryByText(/Atlas Product Launch/)).not.toBeInTheDocument();
    expect(screen.getByText('Past events (1)')).toBeInTheDocument();
  });

  it("tells crew who aren't on anything that their manager will add them", async () => {
    renderApp({ me: users.casualCrew, route: '/my/events' });

    expect(await screen.findByRole('heading', { name: "You're not on any events yet" })).toBeInTheDocument();
    expect(screen.getByText('Your manager will add you.')).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = renderMockedApp('crewLead', '/my/events');

    await screen.findByRole('article', { name: 'Riverlight Festival' });
    await screen.findByText('Hi-vis at all times during build.');
    expect(await axeViolations(container)).toEqual([]);
  });
});

describe('CrewEventPage', () => {
  it('shows the day as crew need it, with no money anywhere', async () => {
    const { container } = renderMockedApp('crewLead', `/my/events/${naidoo.eventId}`);

    expect(await screen.findByRole('heading', { level: 1, name: 'Naidoo Wedding' })).toBeInTheDocument();
    expect(screen.getByText(/Do not photograph or post content from this event\./)).toBeInTheDocument();
    expect(await screen.findByText(callTimeAt(naidoo))).toBeInTheDocument();

    const milestones = await screen.findByRole('region', { name: 'Milestones' });
    expect(await within(milestones).findByText('Load-in')).toBeInTheDocument();
    expect(within(milestones).getByText('Strike')).toBeInTheDocument();
    expect(within(milestones).queryByText('Invoice')).not.toBeInTheDocument();
    expect(within(milestones).queryByRole('button')).not.toBeInTheDocument();

    const schedule = await screen.findByRole('region', { name: 'Service schedule' });
    expect(within(schedule).getAllByText(/Bars will be packed/)).toHaveLength(2);

    const tasks = await screen.findByRole('region', { name: 'My tasks for this event' });
    expect(await within(tasks).findByRole('link', { name: 'Set up the bar' })).toBeInTheDocument();

    expect(
      await screen.findByText('Sign in at the gatehouse with ID. Collect contractor bibs from security.'),
    ).toBeInTheDocument();
    expect(container).not.toHaveTextContent(/R\s?\d/);
    expect(screen.queryByRole('region', { name: 'Costing' })).not.toBeInTheDocument();
  });

  it('offers to report against the event', async () => {
    renderMockedApp('casualCrew', `/my/events/${naidoo.eventId}`);

    expect(await screen.findByRole('link', { name: 'Report breakage or shortfall' })).toHaveAttribute(
      'href',
      `/my/report?event=${naidoo.eventId}`,
    );
  });
});
