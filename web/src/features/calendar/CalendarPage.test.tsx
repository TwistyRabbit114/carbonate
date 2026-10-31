import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { axeViolations } from '@/test/axe';
import { demoEvents } from '@/test/fixtures/events';
import { server } from '@/test/msw/server';
import { renderMockedApp } from '@/test/renderMockedApp';
import { addDays, addMonths, longDay, monthTitle, todayInSast, weekTitle } from './calendarDates';

const vantage = demoEvents().find((event) => event.eventCode === 'VAN-ACT-26')!;
const atlas = demoEvents().find((event) => event.eventCode === 'ATL-LNCH-26')!; //still an enquiry

const dayButton = (date: string) => screen.getByRole('button', { name: new RegExp(`^${longDay(date)}`) });
const dayCell = (date: string) => dayButton(date).closest('td')!;
const loaded = () =>
  waitFor(() => expect(screen.queryByText('Loading the calendar…')).not.toBeInTheDocument());

afterEach(() => {
  vi.unstubAllGlobals();
});

//----------------------------------------------------------\\
//                              MONTH
//----------------------------------------------------------\\

describe('CalendarPage month', () => {
  //vantage's day holds its load-in at 04:00, rehearsal at 08:00, the event at 10:00, then strike
  //and load-out. two chips fit: the event first, then the earliest milestone, and "+3 more"
  it('puts each event and its milestones on their days, with nothing for axe to flag', async () => {
    const { container } = renderMockedApp('eventManager', `/calendar?view=month&date=${vantage.eventDate}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: monthTitle(vantage.eventDate) }),
    ).toBeInTheDocument();
    const cell = dayCell(vantage.eventDate);
    //milestones arrive just after the events, so wait for them before looking at the cell
    expect(
      await within(cell).findByRole('link', { name: 'Load-in · Vantage Brand Activation' }),
    ).toBeInTheDocument();
    expect(within(cell).getByRole('link', { name: 'Vantage Brand Activation' })).toHaveAttribute(
      'href',
      `/events/${vantage.eventId}`,
    );
    expect(within(cell).queryByRole('link', { name: /Rehearsal/ })).not.toBeInTheDocument();
    expect(within(cell).getByRole('button', { name: /^3 more on/ })).toBeInTheDocument();

    //the chosen day's full list sits under the grid, doors is left to the event itself
    const day = screen.getByRole('region', { name: longDay(vantage.eventDate) });
    expect(
      await within(day).findByRole('link', { name: '18:00 Strike · Vantage Brand Activation' }),
    ).toBeInTheDocument();
    expect(
      within(day).getByRole('link', { name: /Load-out · Vantage Brand Activation/ }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/Doors ·/)).not.toBeInTheDocument();
    expect(await axeViolations(container)).toEqual([]);
  });

  it('leaves enquiries off, as the events board does', async () => {
    renderMockedApp('eventManager', `/calendar?view=month&date=${atlas.eventDate}`);
    await screen.findByRole('heading', { level: 1, name: monthTitle(atlas.eventDate) });
    await loaded();

    expect(screen.queryByRole('link', { name: /Atlas Product Launch/ })).not.toBeInTheDocument();
  });

  it('opens the whole day from "+3 more"', async () => {
    const user = userEvent.setup();
    const other = addDays(vantage.eventDate, 1);
    renderMockedApp('eventManager', `/calendar?view=month&date=${other}`);
    await screen.findByRole('heading', { level: 1, name: monthTitle(other) });

    await user.click(await within(dayCell(vantage.eventDate)).findByRole('button', { name: /^3 more on/ }));

    const heading = screen.getByRole('heading', { level: 2, name: longDay(vantage.eventDate) });
    expect(heading).toHaveFocus();
    expect(
      within(screen.getByRole('region', { name: longDay(vantage.eventDate) })).getByRole('link', {
        name: /Load-out · Vantage Brand Activation/,
      }),
    ).toBeInTheDocument();
  });

  it('says so when a month is empty', async () => {
    renderMockedApp('eventManager', '/calendar?view=month&date=2031-03-10');

    expect(await screen.findByText('Nothing on the calendar this month yet.')).toBeInTheDocument();
  });

  it('shows a way to retry when the events fail to load', async () => {
    renderMockedApp('eventManager', '/calendar?view=month');
    server.use(http.get('/api/events', () => HttpResponse.json({ status: 404 }, { status: 404 })));

    expect(await screen.findByText("We couldn't load the calendar.")).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});

//----------------------------------------------------------\\
//                              MOVING AROUND
//----------------------------------------------------------\\

describe('CalendarPage moving around', () => {
  it('pages through months and comes back to today', async () => {
    const user = userEvent.setup();
    renderMockedApp('eventManager', `/calendar?view=month&date=${vantage.eventDate}`);
    await screen.findByRole('heading', { level: 1, name: monthTitle(vantage.eventDate) });

    await user.click(screen.getByRole('button', { name: 'Next month' }));
    expect(
      screen.getByRole('heading', { level: 1, name: monthTitle(addMonths(vantage.eventDate, 1)) }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Previous month' }));
    await user.click(screen.getByRole('button', { name: 'Previous month' }));
    expect(
      screen.getByRole('heading', { level: 1, name: monthTitle(addMonths(vantage.eventDate, -1)) }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Today' }));
    expect(screen.getByRole('heading', { level: 1, name: monthTitle(todayInSast()) })).toBeInTheDocument();
    expect(dayButton(todayInSast())).toHaveAttribute('aria-current', 'date');
  });

  it('walks the days with the arrow keys and carries on into the next month', async () => {
    const user = userEvent.setup();
    const lastDay = addDays(addMonths(vantage.eventDate, 1), -1);
    renderMockedApp('eventManager', `/calendar?view=month&date=${lastDay}`);
    await screen.findByRole('heading', { level: 1, name: monthTitle(lastDay) });

    await user.click(dayButton(lastDay));
    await user.keyboard('{ArrowRight}');

    const first = addDays(lastDay, 1);
    expect(screen.getByRole('heading', { level: 1, name: monthTitle(first) })).toBeInTheDocument();
    await waitFor(() => expect(dayButton(first)).toHaveFocus());
    await user.keyboard('{ArrowDown}');
    await waitFor(() => expect(dayButton(addDays(first, 7))).toHaveFocus());
    //only the chosen day is a tab stop, the rest are reached with the arrows
    expect(dayButton(addDays(first, 7))).toHaveAttribute('tabindex', '0');
    expect(dayButton(first)).toHaveAttribute('tabindex', '-1');
  });
});

//----------------------------------------------------------\\
//                              WEEK
//----------------------------------------------------------\\

describe('CalendarPage week', () => {
  it('lists the seven days with the time of each item', async () => {
    renderMockedApp('eventManager', `/calendar?view=week&date=${vantage.eventDate}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: weekTitle(vantage.eventDate) }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(7);
    const event = await screen.findByRole('link', { name: '10:00 Vantage Brand Activation' });
    expect(event).toHaveAttribute('href', `/events/${vantage.eventId}`);
  });

  it('starts on the week for crew on a phone, and sends them to My events', async () => {
    vi.stubGlobal(
      'matchMedia',
      (query: string) =>
        ({
          matches: query === '(max-width: 760px)',
          media: query,
          addEventListener: () => undefined,
          removeEventListener: () => undefined,
        }) as unknown as MediaQueryList,
    );
    renderMockedApp('crewLead', `/calendar?date=${vantage.eventDate}`);

    expect(
      await screen.findByRole('heading', { level: 1, name: weekTitle(vantage.eventDate) }),
    ).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: '10:00 Vantage Brand Activation' })).toHaveAttribute(
      'href',
      `/my/events/${vantage.eventId}`,
    );
  });
});
